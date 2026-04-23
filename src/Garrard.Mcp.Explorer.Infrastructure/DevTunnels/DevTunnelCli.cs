using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class DevTunnelCli : IDevTunnelCli
{
    private static readonly Regex TunnelUrlRegex = new(@"https://[^\s,]+devtunnels\.ms[^\s,]*", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly string _executablePath;
    private readonly ILogger<DevTunnelCli> _logger;

    public DevTunnelCli(IConfiguration configuration, ILogger<DevTunnelCli> logger)
    {
        _logger = logger;
        _executablePath = configuration["DevTunnels:ExecutablePath"]
            ?? Environment.GetEnvironmentVariable("DEVTUNNELS__ExecutablePath")
            ?? "devtunnel";
    }

    public async Task<DevTunnelUserState> GetUserStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunCommandAsync(["user", "show"], cancellationToken).ConfigureAwait(false);
            return ParseUserState(result.ExitCode, result.Output);
        }
        catch (DevTunnelCliUnavailableException ex)
        {
            return new DevTunnelUserState(IsLoggedIn: false, UserName: null, Provider: null)
            {
                IsAvailable = false,
                Detail = ex.Message
            };
        }
    }

    public async IAsyncEnumerable<string> LoginWithDeviceCodeAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in StreamCommandAsync(
            ["user", "login", "--use-device-code-auth"],
            commandName: "devtunnel user login",
            cancellationToken).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    public async Task<string> EnsureTunnelExistsAsync(
        string tunnelName,
        TunnelAccess access,
        int port,
        string? existingCliTunnelId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelName);

        // We let the Dev Tunnels service mint the tunnel ID (e.g. "fun-plane-vc21wbv") — short
        // custom IDs have historically collided with phantom/stale service records and cause
        // "Tunnel not found" errors on host/delete even when `show` reports them. The service ID
        // is the only one that reliably round-trips through create → port → host.
        var cliTunnelId = existingCliTunnelId;

        if (string.IsNullOrWhiteSpace(cliTunnelId))
        {
            var createArgs = new List<string> { "create", "--description", tunnelName, "--json" };
            if (access == TunnelAccess.Anonymous)
                createArgs.Add("--allow-anonymous");

            var createResult = await RunCommandAsync(createArgs, cancellationToken).ConfigureAwait(false);
            if (createResult.ExitCode != 0)
                throw CreateCommandFailure("devtunnel create", createResult.Output);

            cliTunnelId = ParseTunnelIdFromCreateOutput(createResult.Output)
                ?? throw new InvalidOperationException(
                    "Could not parse tunnel ID from devtunnel create output: " + createResult.Output);
        }

        // Always delete-then-create the port, so if a tunnel was previously registered
        // with a different protocol (e.g. "https" from older versions of this code) the
        // protocol gets refreshed. `port delete` is idempotent (404 on missing port).
        await RunCommandAsync(
            ["port", "delete", cliTunnelId, "--port-number", port.ToString(), "--force"],
            cancellationToken).ConfigureAwait(false);

        var portArgs = new List<string>
        {
            "port", "create", cliTunnelId,
            "--port-number", port.ToString(),
            // This describes the LOCAL backend's protocol, not the tunnel URL. Kestrel in
            // the API container serves plain HTTP on :5000; the Dev Tunnels relay still
            // exposes it over HTTPS at the public tunnel URL and does TLS termination.
            // Declaring "https" here makes the relay attempt a TLS handshake with a plain
            // HTTP socket, which fails with 502 Bad Gateway for every inbound request.
            "--protocol", "http",
            "--json"
        };
        var portResult = await RunCommandAsync(portArgs, cancellationToken).ConfigureAwait(false);
        if (portResult.ExitCode != 0 && !LooksLikeAlreadyExists(portResult.Output))
            throw CreateCommandFailure("devtunnel port create", portResult.Output);

        return cliTunnelId;
    }

    private static string? ParseTunnelIdFromCreateOutput(string output)
    {
        // `devtunnel create --json` prints a license banner first then the JSON payload.
        // Scan forward from the first '{' to the last '}' to parse cleanly.
        var start = output.IndexOf('{');
        var end = output.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        try
        {
            using var doc = JsonDocument.Parse(output[start..(end + 1)]);
            if (!doc.RootElement.TryGetProperty("tunnel", out var tunnelElement))
                return null;
            if (!tunnelElement.TryGetProperty("tunnelId", out var idElement))
                return null;
            var fullId = idElement.GetString();
            if (string.IsNullOrWhiteSpace(fullId))
                return null;

            // The CLI returns "<id>.<cluster>" (e.g. "fun-plane-vc21wbv.uks1"). All subsequent
            // commands accept the short form and will pick the default cluster from login.
            var dot = fullId.IndexOf('.');
            return dot > 0 ? fullId[..dot] : fullId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async IAsyncEnumerable<DevTunnelHostUpdate> HostAsync(
        string tunnelId,
        TunnelAccess access,
        int port,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        _ = access;
        _ = port;

        // With the persistent create-first flow, hosting is identical for anonymous and
        // authenticated tunnels — the port and access control already live on the tunnel
        // record. The CLI prints `Connect via browser: https://<id>.<region>.devtunnels.ms:<port>`
        // which is parsed out by TryParseTunnelUri.
        var arguments = new[] { "host", tunnelId };

        await foreach (var line in StreamCommandAsync(
            arguments,
            commandName: "devtunnel host",
            cancellationToken).ConfigureAwait(false))
        {
            yield return new DevTunnelHostUpdate(
                TimestampUtc: DateTime.UtcNow,
                OutputLine: line,
                Status: InferHostStatus(line),
                TunnelUri: TryParseTunnelUri(line));
        }
    }

    public async Task DeleteTunnelAsync(string tunnelId, TunnelAccess access, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tunnelId);
        _ = access;

        var result = await RunCommandAsync(["delete", tunnelId, "--force"], cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0 && !LooksLikeMissingTunnel(result.Output))
            throw CreateCommandFailure("devtunnel delete", result.Output);
    }

    internal static DevTunnelUserState ParseUserState(int exitCode, string output)
    {
        if (exitCode != 0 || LooksLoggedOut(output))
            return new DevTunnelUserState(IsLoggedIn: false, UserName: null, Provider: null);

        string? userName = null;
        string? provider = null;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("User:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Account:", StringComparison.OrdinalIgnoreCase))
            {
                userName = line[(line.IndexOf(':') + 1)..].Trim();
            }

            if (line.StartsWith("Provider:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Login provider:", StringComparison.OrdinalIgnoreCase))
            {
                provider = line[(line.IndexOf(':') + 1)..].Trim();
            }
        }

        return new DevTunnelUserState(IsLoggedIn: true, UserName: userName, Provider: provider);
    }

    internal static Uri? TryParseTunnelUri(string line)
    {
        // `devtunnel host` prints multiple URLs. The "-inspect" subdomain is Microsoft's
        // in-browser request-inspection dashboard and does NOT forward traffic to the
        // hosted port — posting to it hits the inspector UI instead of our API. Skip
        // lines that are explicitly about inspection, and if a match is an inspect URL,
        // look for a later non-inspect URL on the same line.
        if (line.Contains("Inspect network activity", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (Match match in TunnelUrlRegex.Matches(line))
        {
            if (match.Value.Contains("-inspect.", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Uri.TryCreate(match.Value.TrimEnd('/', '.', ','), UriKind.Absolute, out var uri))
                return uri;
        }

        return null;
    }

    internal static TunnelStatus InferHostStatus(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return TunnelStatus.Starting;

        if (line.Contains("Connect via browser:", StringComparison.OrdinalIgnoreCase)
            || line.Contains(".devtunnels.ms", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Ready to accept connections", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Hosting port", StringComparison.OrdinalIgnoreCase))
            return TunnelStatus.Running;

        if (line.Contains("login", StringComparison.OrdinalIgnoreCase)
            || line.Contains("expired", StringComparison.OrdinalIgnoreCase)
            || line.Contains("sign in", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not permitted", StringComparison.OrdinalIgnoreCase)
            || line.Contains("access scope", StringComparison.OrdinalIgnoreCase))
            return TunnelStatus.LoginRequired;

        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            return TunnelStatus.Error;

        return TunnelStatus.Starting;
    }

    private async Task<CommandResult> RunCommandAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = StartProcess(arguments);
        using var registration = cancellationToken.Register(() => TryKill(process));

        var stdoutTask = ReadAllLinesAsync(process.StandardOutput);
        var stderrTask = ReadAllLinesAsync(process.StandardError);

        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        var stdoutLines = await stdoutTask.ConfigureAwait(false);
        var stderrLines = await stderrTask.ConfigureAwait(false);
        var output = string.Join(Environment.NewLine, stdoutLines.Concat(stderrLines));

        return new CommandResult(process.ExitCode, output);
    }

    private async IAsyncEnumerable<string> StreamCommandAsync(
        IEnumerable<string> arguments,
        string commandName,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        bool throwOnFailure = true)
    {
        using var process = StartProcess(arguments);
        var channel = Channel.CreateUnbounded<string>();
        var observed = new ConcurrentQueue<string>();

        var stdoutTask = PumpReaderAsync(process.StandardOutput, channel.Writer, observed, cancellationToken);
        var stderrTask = PumpReaderAsync(process.StandardError, channel.Writer, observed, cancellationToken);

        using var registration = cancellationToken.Register(() => TryKill(process));

        _logger.LogInformation("Starting {Command}: {Executable} {Arguments}", commandName, _executablePath, string.Join(' ', arguments));

        var completionTask = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested && process.ExitCode != 0 && throwOnFailure)
                {
                    channel.Writer.TryComplete(CreateCommandFailure(commandName, string.Join(Environment.NewLine, observed)));
                    return;
                }

                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        await foreach (var line in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return line;

        await completionTask.ConfigureAwait(false);
    }

    private Process StartProcess(IEnumerable<string> arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start())
                throw new InvalidOperationException($"Could not start '{_executablePath}'.");

            return process;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new DevTunnelCliUnavailableException(
                $"The devtunnel CLI could not be started from '{_executablePath}'. Install Dev Tunnels or configure DevTunnels:ExecutablePath.",
                ex);
        }
    }

    private static async Task PumpReaderAsync(
        StreamReader reader,
        ChannelWriter<string> writer,
        ConcurrentQueue<string> observed,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    break;

                observed.Enqueue(line);
                await writer.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static async Task<List<string>> ReadAllLinesAsync(StreamReader reader)
    {
        var lines = new List<string>();

        while (true)
        {
            var line = await reader.ReadLineAsync().ConfigureAwait(false);
            if (line is null)
                break;

            lines.Add(line);
        }

        return lines;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
        }
    }

    private static InvalidOperationException CreateCommandFailure(string commandName, string output)
    {
        var summary = string.IsNullOrWhiteSpace(output) ? "No output was produced." : output.Trim();
        return new InvalidOperationException($"{commandName} failed. {summary}");
    }

    private static bool LooksLoggedOut(string output)
        => output.Contains("expired", StringComparison.OrdinalIgnoreCase)
           || output.Contains("not logged in", StringComparison.OrdinalIgnoreCase)
           || output.Contains("sign in", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeAlreadyExists(string output)
        => output.Contains("already exists", StringComparison.OrdinalIgnoreCase)
           || output.Contains("conflict", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMissingTunnel(string output)
        => output.Contains("not found", StringComparison.OrdinalIgnoreCase)
           || output.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private sealed record CommandResult(int ExitCode, string Output);
}
