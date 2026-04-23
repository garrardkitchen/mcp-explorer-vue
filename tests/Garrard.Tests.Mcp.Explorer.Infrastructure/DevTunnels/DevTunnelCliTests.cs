using System.Runtime.CompilerServices;
using Garrard.Mcp.Explorer.Core.Domain.DevTunnels;
using Garrard.Mcp.Explorer.Infrastructure.DevTunnels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Garrard.Tests.Mcp.Explorer.Infrastructure.DevTunnels;

public sealed class DevTunnelCliTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _scriptPath;
    private readonly string _argsLogPath;

    public DevTunnelCliTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "mcp-explorer-devtunnel-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);

        _scriptPath = Path.Combine(_testDir, "devtunnel");
        _argsLogPath = Path.Combine(_testDir, "args.log");
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task GetUserStateAsync_WhenTokenExpired_ReturnsLoggedOut()
    {
        WriteScript("""
#!/bin/bash
if [[ "$1" == "user" && "$2" == "show" ]]; then
  echo "Login token expired."
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var state = await sut.GetUserStateAsync();

        Assert.False(state.IsLoggedIn);
        Assert.Null(state.UserName);
    }

    [Fact]
    public async Task GetUserStateAsync_WhenLoggedIn_ParsesMetadata()
    {
        WriteScript("""
#!/bin/bash
if [[ "$1" == "user" && "$2" == "show" ]]; then
  echo "Name: Demo User"
  echo "Provider: GitHub"
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var state = await sut.GetUserStateAsync();

        Assert.True(state.IsLoggedIn);
        Assert.Equal("Demo User", state.UserName);
        Assert.Equal("GitHub", state.Provider);
    }

    [Fact]
    public async Task GetUserStateAsync_WhenExecutableMissing_ReturnsUnavailableState()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:ExecutablePath"] = Path.Combine(_testDir, "missing-devtunnel")
            })
            .Build();

        var sut = new DevTunnelCli(configuration, NullLogger<DevTunnelCli>.Instance);

        var state = await sut.GetUserStateAsync();

        Assert.False(state.IsLoggedIn);
        Assert.False(state.IsAvailable);
        Assert.Contains("could not be started", state.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureTunnelExistsAsync_Anonymous_AddsAllowAnonymousFlag()
    {
        WriteScript($$$"""
#!/bin/bash
echo "$@" >> "{{{_argsLogPath}}}"
if [[ "$1" == "create" ]]; then
  echo '{"tunnel":{"tunnelId":"generated-id.uks1"}}'
  exit 0
fi
if [[ "$1" == "port" && "$2" == "create" ]]; then
  echo '{"portNumber":5000}'
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var cliId = await sut.EnsureTunnelExistsAsync("Tunnel One", TunnelAccess.Anonymous, 5000);

        Assert.Equal("generated-id", cliId);
        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.Contains(loggedArgs, line => line.Contains("create --description Tunnel One --json --allow-anonymous", StringComparison.Ordinal));
        Assert.Contains(loggedArgs, line => line.Contains("port create generated-id --port-number 5000 --protocol http --json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnsureTunnelExistsAsync_PassesAuthenticatedCreateAndPortArguments()
    {
        WriteScript($$$"""
#!/bin/bash
echo "$@" >> "{{{_argsLogPath}}}"
if [[ "$1" == "create" ]]; then
  echo '{"tunnel":{"tunnelId":"generated-id.uks1"}}'
  exit 0
fi
if [[ "$1" == "port" && "$2" == "create" ]]; then
  echo '{"portNumber":5000}'
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var cliId = await sut.EnsureTunnelExistsAsync("Tunnel One", TunnelAccess.Authenticated, 5000);

        Assert.Equal("generated-id", cliId);
        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.Contains(loggedArgs, line => line.Contains("create --description Tunnel One --json", StringComparison.Ordinal));
        Assert.DoesNotContain(loggedArgs, line => line.Contains("--allow-anonymous", StringComparison.Ordinal));
        Assert.Contains(loggedArgs, line => line.Contains("port create generated-id --port-number 5000 --protocol http --json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnsureTunnelExistsAsync_WithExistingCliId_SkipsCreate()
    {
        WriteScript($$"""
#!/bin/bash
echo "$@" >> "{{_argsLogPath}}"
if [[ "$1" == "port" && "$2" == "create" ]]; then
  echo '{"portNumber":5000}'
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var cliId = await sut.EnsureTunnelExistsAsync("Tunnel One", TunnelAccess.Anonymous, 5000, existingCliTunnelId: "abc");

        Assert.Equal("abc", cliId);
        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.DoesNotContain(loggedArgs, line => line.StartsWith("create", StringComparison.Ordinal));
        Assert.Contains(loggedArgs, line => line.Contains("port create abc --port-number 5000 --protocol http --json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoginWithDeviceCodeAsync_StreamsOutputLines()
    {
        WriteScript("""
#!/bin/bash
if [[ "$1" == "user" && "$2" == "login" ]]; then
  echo "Visit https://microsoft.com/devicelogin"
  echo "Enter code ABCD-EFGH"
  exit 0
fi
exit 1
""");

        var sut = CreateSut();

        var lines = await ToListAsync(sut.LoginWithDeviceCodeAsync());

        Assert.Contains("Visit https://microsoft.com/devicelogin", lines);
        Assert.Contains("Enter code ABCD-EFGH", lines);
    }

    [Fact]
    public async Task HostAsync_Anonymous_HostsByIdSameAsAuthenticated()
    {
        WriteScript($$"""
#!/bin/bash
echo "$@" >> "{{_argsLogPath}}"
echo "Connect via browser: https://abc-5000.uks1.devtunnels.ms/"
exit 0
""");

        var sut = CreateSut();

        var updates = await ToListAsync(sut.HostAsync("t-1", TunnelAccess.Anonymous, 5000));

        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.Contains(loggedArgs, line => line.Contains("host t-1", StringComparison.Ordinal));
        Assert.Equal(TunnelStatus.Running, updates[0].Status);
        Assert.Equal(new Uri("https://abc-5000.uks1.devtunnels.ms"), updates[0].TunnelUri);
    }

    [Fact]
    public async Task HostAsync_Authenticated_HostsByIdAndParsesUrl()
    {
        WriteScript($$"""
#!/bin/bash
echo "$@" >> "{{_argsLogPath}}"
echo "Connect via browser: https://abc.devtunnels.ms/"
echo "Listening for requests"
exit 0
""");

        var sut = CreateSut();

        var updates = await ToListAsync(sut.HostAsync("t-1", TunnelAccess.Authenticated, 5000));

        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.Contains(loggedArgs, line => line.Contains("host t-1", StringComparison.Ordinal));
        Assert.Equal(2, updates.Count);
        Assert.Equal(TunnelStatus.Running, updates[0].Status);
        Assert.Equal(new Uri("https://abc.devtunnels.ms"), updates[0].TunnelUri);
        Assert.Equal(TunnelStatus.Starting, updates[1].Status);
    }

    [Fact]
    public async Task HostAsync_SkipsInspectUrlAndKeepsRealTunnelUri()
    {
        WriteScript($$"""
#!/bin/bash
echo "$@" >> "{{_argsLogPath}}"
echo "Hosting port: 5000"
echo "Connect via browser: https://abc.uks1.devtunnels.ms:5000, https://abc-5000.uks1.devtunnels.ms"
echo "Inspect network activity: https://abc-5000-inspect.uks1.devtunnels.ms"
echo "Ready to accept connections for tunnel: abc"
exit 0
""");

        var sut = CreateSut();

        var updates = await ToListAsync(sut.HostAsync("abc", TunnelAccess.Anonymous, 5000));

        // The "Inspect network activity" line must NOT set TunnelUri (it would point at
        // the devtunnels request-inspector dashboard, not the forwarded API).
        var nonNullUris = updates.Where(u => u.TunnelUri is not null).Select(u => u.TunnelUri!.ToString()).ToList();
        Assert.All(nonNullUris, uri => Assert.DoesNotContain("-inspect", uri, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(nonNullUris, uri => uri.Contains("abc.uks1.devtunnels.ms:5000", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DeleteTunnelAsync_Anonymous_InvokesDelete()
    {
        WriteScript($$"""
#!/bin/bash
echo "$@" >> "{{_argsLogPath}}"
exit 0
""");

        var sut = CreateSut();

        await sut.DeleteTunnelAsync("t-1", TunnelAccess.Anonymous);

        var loggedArgs = await File.ReadAllLinesAsync(_argsLogPath);
        Assert.Contains(loggedArgs, line => line.Contains("delete t-1 --force", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteTunnelAsync_IgnoresMissingTunnel()
    {
        WriteScript("""
#!/bin/bash
if [[ "$1" == "delete" ]]; then
  echo "Tunnel does not exist."
  exit 1
fi
exit 1
""");

        var sut = CreateSut();

        await sut.DeleteTunnelAsync("t-1", TunnelAccess.Authenticated);
    }

    private DevTunnelCli CreateSut()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DevTunnels:ExecutablePath"] = _scriptPath
            })
            .Build();

        return new DevTunnelCli(configuration, NullLogger<DevTunnelCli>.Instance);
    }

    private void WriteScript(string content)
    {
        File.WriteAllText(_scriptPath, content.Replace("\r\n", "\n"));
        using var chmod = System.Diagnostics.Process.Start("chmod", $"+x {_scriptPath}");
        chmod?.WaitForExit();
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        var items = new List<T>();
        await foreach (var item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            items.Add(item);
        return items;
    }
}
