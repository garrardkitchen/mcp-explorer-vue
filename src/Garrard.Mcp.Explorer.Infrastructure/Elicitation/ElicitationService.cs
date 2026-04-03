using System.Collections.Concurrent;
using System.Text.Json;
using Garrard.Mcp.Explorer.Core.Domain.Elicitation;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Garrard.Mcp.Explorer.Infrastructure.Elicitation;

/// <summary>
/// Server-initiated elicitation request handler. Wires MCP elicitation delegates to
/// UI-level completion sources and maintains request history.
/// </summary>
public sealed class ElicitationService : IElicitationService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private readonly ILogger<ElicitationService> _logger;
    private readonly TimeSpan? _timeout;

    private readonly ConcurrentDictionary<string, PendingElicitationContext> _pending =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<ElicitationHistoryEntry> _history = [];
    private readonly object _historyLock = new();

    public event EventHandler<ElicitationRequestedEventArgs>? ElicitationRequested;

    public ElicitationService(ILogger<ElicitationService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var timeoutSeconds = configuration.GetValue<int?>("Elicitation:TimeoutSeconds") ?? 0;
        _timeout = timeoutSeconds > 0 ? TimeSpan.FromSeconds(timeoutSeconds) : null;
    }

    /// <summary>
    /// Called from the MCP <c>ElicitationHandler</c> delegate in <c>ConnectionService</c>.
    /// Suspends until the UI submits a response, times out, or the token is cancelled.
    /// </summary>
    public async ValueTask<ElicitResult> HandleElicitationRequestAsync(
        string connectionName,
        ElicitRequestParams? requestParams,
        CancellationToken cancellationToken)
    {
        if (requestParams?.RequestedSchema?.Properties is null)
            return new ElicitResult { Action = "reject" };

        var requestId = Guid.NewGuid().ToString("N");

        var schema = new Dictionary<string, object>();
        foreach (var kvp in requestParams.RequestedSchema.Properties)
            schema[kvp.Key] = kvp.Value;

        var request = new ElicitationRequest(
            requestId,
            connectionName,
            DateTime.UtcNow,
            requestParams.Message,
            schema,
            ElicitationStatus.Pending);

        var tcs = new TaskCompletionSource<ElicitResult>();
        var context = new PendingElicitationContext(request, tcs);

        if (!_pending.TryAdd(requestId, context))
            return new ElicitResult { Action = "reject" };

        try
        {
            ElicitationRequested?.Invoke(this, new ElicitationRequestedEventArgs(request));

            ElicitResult result;
            if (_timeout.HasValue)
            {
                try
                {
                    result = await tcs.Task.WaitAsync(_timeout.Value, cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    throw new OperationCanceledException("Elicitation request timed out.");
                }
            }
            else
            {
                result = await tcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            var finalStatus = result.Action == "accept" ? ElicitationStatus.Accepted : ElicitationStatus.Rejected;
            var response = new ElicitationResponse(
                requestId,
                DateTime.UtcNow,
                result.Action,
                result.Content != null ? new Dictionary<string, JsonElement>(result.Content) : null);

            lock (_historyLock)
                _history.Add(new ElicitationHistoryEntry(request with { Status = finalStatus }, response));

            return result;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Elicitation request {RequestId} was cancelled or timed out", requestId);

            var response = new ElicitationResponse(requestId, DateTime.UtcNow, "reject", null);
            lock (_historyLock)
                _history.Add(new ElicitationHistoryEntry(
                    request with { Status = ElicitationStatus.Rejected }, response));

            return new ElicitResult { Action = "reject" };
        }
        finally
        {
            _pending.TryRemove(requestId, out _);
        }
    }

    public IReadOnlyList<ElicitationRequest> GetPendingRequests(string? connectionName = null)
    {
        var pending = _pending.Values.Select(c => c.Request);
        if (!string.IsNullOrEmpty(connectionName))
            pending = pending.Where(r => string.Equals(r.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase));
        return pending.OrderBy(r => r.TimestampUtc).ToList();
    }

    public IReadOnlyList<ElicitationHistoryEntry> GetHistory(string? connectionName = null)
    {
        lock (_historyLock)
        {
            var history = _history.AsEnumerable();
            if (!string.IsNullOrEmpty(connectionName))
                history = history.Where(h => string.Equals(h.Request.ConnectionName, connectionName, StringComparison.OrdinalIgnoreCase));
            return history.OrderByDescending(h => h.Request.TimestampUtc).ToList();
        }
    }

    public Task<bool> SubmitResponseAsync(
        string requestId,
        string action,
        Dictionary<string, JsonElement>? content,
        CancellationToken cancellationToken = default)
    {
        if (!_pending.TryGetValue(requestId, out var ctx))
            return Task.FromResult(false);

        ctx.CompletionSource.TrySetResult(new ElicitResult { Action = action, Content = content });
        return Task.FromResult(true);
    }

    private sealed class PendingElicitationContext(ElicitationRequest request, TaskCompletionSource<ElicitResult> completionSource)
    {
        public ElicitationRequest Request { get; } = request;
        public TaskCompletionSource<ElicitResult> CompletionSource { get; } = completionSource;
    }
}
