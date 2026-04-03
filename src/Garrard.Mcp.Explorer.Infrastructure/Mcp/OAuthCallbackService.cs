using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Garrard.Mcp.Explorer.Infrastructure.Mcp;

/// <summary>
/// Bridges the OAuth <c>AuthorizationRedirectDelegate</c> (running on the MCP client thread)
/// with the application's OAuth callback endpoint.
///
/// Only one pending OAuth flow at a time is supported.
/// </summary>
public sealed class OAuthCallbackService(ILogger<OAuthCallbackService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile TaskCompletionSource<Uri>? _pendingTcs;

    /// <summary>
    /// Opens the browser at <paramref name="authorizationUri"/>, waits for the OAuth callback,
    /// and returns the redirect URI delivered by <see cref="CompleteCallback"/>.
    /// </summary>
    public async Task<Uri> AwaitCallbackAsync(
        Uri authorizationUri,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var tcs = new TaskCompletionSource<Uri>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingTcs = tcs;

        OpenBrowser(authorizationUri);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));

        logger.LogInformation("[OAuth] Waiting for callback (timeout {Timeout}s). Auth URL: {Url}",
            (int)timeout.TotalSeconds, authorizationUri);

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pendingTcs = null;
            _gate.Release();
        }
    }

    /// <summary>Called by the OAuth callback endpoint with the full redirect URI.</summary>
    public void CompleteCallback(Uri redirectedUri)
    {
        var tcs = Interlocked.Exchange(ref _pendingTcs, null);
        if (tcs is null)
        {
            logger.LogWarning("[OAuth] Received callback but no pending flow. URI: {Uri}", redirectedUri);
            return;
        }

        logger.LogInformation("[OAuth] Callback received: {Uri}", redirectedUri);
        tcs.TrySetResult(redirectedUri);
    }

    private void OpenBrowser(Uri uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
            logger.LogInformation("[OAuth] Opened browser for authorization");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[OAuth] Could not open browser automatically. Please visit: {Url}", uri);
        }
    }
}
