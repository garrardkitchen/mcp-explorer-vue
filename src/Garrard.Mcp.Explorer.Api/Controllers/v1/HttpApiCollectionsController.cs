using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.HttpApis;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.HttpApi;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/http-api-collections")]
public sealed class HttpApiCollectionsController(
    IHttpApiStore store,
    IHttpApiSnapshotStore snapshotStore,
    IHttpApiInvoker invoker,
    ISchemaInferenceService schemaInference,
    ISchemaComparisonService schemaComparison) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var collections = await store.GetAllCollectionsAsync(ct);
        return Ok(collections.OrderBy(c => c.Name));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(string id, CancellationToken ct)
    {
        var collection = await store.GetCollectionAsync(id, ct);
        if (collection is null) return NotFound();
        return Ok(collection);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveHttpApiCollectionRequest request, CancellationToken ct)
    {
        var all = await store.GetAllCollectionsAsync(ct);
        if (all.Any(c => string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A collection named '{request.Name}' already exists." });

        var collection = new HttpApiCollection
        {
            Name        = request.Name,
            Description = request.Description ?? string.Empty,
            GroupName   = request.GroupName,
            EndpointIds = request.EndpointIds ?? []
        };

        var saved = await store.SaveCollectionAsync(collection, ct);
        return CreatedAtAction(nameof(GetOne), new { id = saved.Id }, saved);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SaveHttpApiCollectionRequest request, CancellationToken ct)
    {
        var existing = await store.GetCollectionAsync(id, ct);
        if (existing is null) return NotFound();

        if (!string.Equals(request.Name, existing.Name, StringComparison.OrdinalIgnoreCase))
        {
            var all = await store.GetAllCollectionsAsync(ct);
            if (all.Any(c => !string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(c.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { error = $"A collection named '{request.Name}' already exists." });
        }

        existing.Name        = request.Name;
        existing.Description = request.Description ?? string.Empty;
        existing.GroupName   = request.GroupName;
        existing.EndpointIds.Clear();
        existing.EndpointIds.AddRange(request.EndpointIds ?? []);

        var saved = await store.SaveCollectionAsync(existing, ct);
        return Ok(saved);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await store.DeleteCollectionAsync(id, ct);
        // Best-effort cleanup of run history
        try { await snapshotStore.DeleteCollectionRunHistoryAsync(id, ct); } catch { /* non-critical */ }
        return NoContent();
    }

    /// <summary>
    /// Runs all endpoints in the collection in order, compares each against its latest baseline snapshot
    /// (if available), and returns a per-endpoint summary.
    /// </summary>
    [HttpPost("{id}/run")]
    public async Task<IActionResult> Run(
        string id,
        [FromBody] RunHttpApiCollectionRequest? request,
        CancellationToken ct)
    {
        var collection = await store.GetCollectionAsync(id, ct);
        if (collection is null) return NotFound();

        var runId     = Guid.NewGuid().ToString();
        var results   = new List<object>();
        var summaries = new List<HttpApiCollectionEndpointRunSummary>();
        var startedAt = DateTime.UtcNow;
        var sw        = System.Diagnostics.Stopwatch.StartNew();

        int successCount = 0;
        int totalCount   = 0;

        foreach (var endpointId in collection.EndpointIds)
        {
            var def = await store.GetDefinitionAsync(endpointId, ct);
            if (def is null)
            {
                results.Add(new { endpointId, error = "Definition not found", skipped = true });
                summaries.Add(new HttpApiCollectionEndpointRunSummary
                {
                    EndpointId   = endpointId,
                    EndpointName = endpointId,
                    Skipped      = true
                });
                continue;
            }

            var invokeResult = await invoker.InvokeAsync(def, request?.Inputs, ct);
            var resolved     = HttpApiTemplateResolver.Apply(def, request?.Inputs);
            var schema       = schemaInference.InferSchema(invokeResult.Body);
            var hash         = schemaInference.ComputeSchemaHash(schema);

            // Get baseline for comparison
            var targetSnapshotId = def.GoldenSnapshotId;
            HttpResponseSnapshot? baseline = null;

            if (!string.IsNullOrEmpty(targetSnapshotId))
                baseline = await snapshotStore.GetSnapshotAsync(endpointId, targetSnapshotId, ct);
            if (baseline is null)
            {
                var allSnaps = await snapshotStore.GetSnapshotsAsync(endpointId, ct);
                baseline = allSnaps.MaxBy(s => s.CapturedAt);
            }

            HttpSchemaComparisonResult? comparison = null;
            if (baseline is not null)
                comparison = schemaComparison.Compare(def, baseline, invokeResult.StatusCode, invokeResult.LatencyMs, schema);

            var isCountedSuccess = invokeResult.IsSuccess && comparison?.IsBreaking != true;
            totalCount++;
            if (isCountedSuccess) successCount++;

            summaries.Add(new HttpApiCollectionEndpointRunSummary
            {
                EndpointId   = def.Id,
                EndpointName = def.Name,
                StatusCode   = invokeResult.StatusCode,
                LatencyMs    = invokeResult.LatencyMs,
                IsSuccess    = isCountedSuccess,
                Skipped      = false
            });

            await snapshotStore.AppendInvocationAsync(new HttpApiInvocationRecord
            {
                EndpointId            = def.Id,
                EndpointName          = def.Name,
                StatusCode            = invokeResult.StatusCode,
                LatencyMs             = invokeResult.LatencyMs,
                SchemaHash            = hash,
                SchemaMatchedSnapshot = comparison is null ? null : !comparison.IsBreaking,
                CollectionRunId       = runId,
                ErrorMessage          = invokeResult.ErrorMessage,
                RequestMethod         = resolved.Method,
                RequestBaseUrl        = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.BaseUrl),
                RequestPath           = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.Path),
                RequestHeaders        = HttpApiInvocationSanitizer.SanitizeHeaders(ToSnapshotDictionary(resolved.Headers.Select(h => (h.Name, h.Value)))),
                RequestQueryParams    = HttpApiInvocationSanitizer.SanitizeQueryParams(ToSnapshotDictionary(resolved.QueryParams.Where(q => q.Enabled).Select(q => (q.Name, q.Value)))),
                ResponseHeaders       = HttpApiInvocationSanitizer.SanitizeHeaders(invokeResult.ResponseHeaders),
                ContentType           = invokeResult.ContentType,
                Body                  = HttpApiInvocationSanitizer.SanitizeBody(invokeResult.TruncatedBody),
                InvokedVia            = HttpApiInvocationSource.App
            }, ct);

            results.Add(new
            {
                endpointId   = def.Id,
                endpointName = def.Name,
                statusCode   = invokeResult.StatusCode,
                latencyMs    = invokeResult.LatencyMs,
                isSuccess    = invokeResult.IsSuccess,
                comparison,
                inferredSchema = schema,
                errorMessage = invokeResult.ErrorMessage
            });
        }

        sw.Stop();
        var durationMs = sw.ElapsedMilliseconds;
        var ranAt      = startedAt;

        await store.PatchCollectionRunStatsAsync(id, ranAt, durationMs, successCount, totalCount, runId, HttpApiInvocationSource.App, summaries, ct);

        // Best-effort: append to collection run history (don't fail the run if this throws)
        try
        {
            await snapshotStore.AppendCollectionRunAsync(new HttpApiCollectionRunRecord
            {
                RunId            = runId,
                CollectionId     = id,
                CollectionName   = collection.Name,
                RanAt            = ranAt,
                DurationMs       = durationMs,
                SuccessCount     = successCount,
                TotalCount       = totalCount,
                InvokedVia       = HttpApiInvocationSource.App,
                EndpointSummaries = summaries
            }, ct);
        }
        catch (Exception ex)
        {
            // Log but swallow — history is non-critical
            _ = ex;
        }

        return Ok(new
        {
            runId,
            collectionId      = id,
            ranAt,
            durationMs,
            successCount,
            totalCount,
            invokedVia        = HttpApiInvocationSource.App,
            endpointSummaries = summaries,
            results
        });
    }

    [HttpGet("{id}/run-history")]
    public async Task<IActionResult> GetRunHistory(string id, [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var history = await snapshotStore.GetCollectionRunHistoryAsync(id, limit, ct);
        return Ok(history);
    }

    [HttpGet("sparklines")]
    public async Task<IActionResult> GetSparklines([FromQuery] int limit = 10, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var data = await snapshotStore.GetCollectionSparklineDataAsync(limit, ct);
        var result = data.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(r => new
            {
                durationMs = r.DurationMs,
                successCount = r.SuccessCount,
                totalCount = r.TotalCount
            }).ToList()
        );
        return Ok(result);
    }

    private static Dictionary<string, string> ToSnapshotDictionary(IEnumerable<(string Name, string Value)> items)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
    }
}
