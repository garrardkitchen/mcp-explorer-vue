using Asp.Versioning;
using Garrard.Mcp.Explorer.Api.Dtos.HttpApis;
using Garrard.Mcp.Explorer.Core.Domain.HttpApi;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.HttpApi;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/http-apis")]
public sealed class HttpApisController(
    IHttpApiStore store,
    IHttpApiSnapshotStore snapshotStore,
    IHttpApiInvoker invoker,
    ISchemaInferenceService schemaInference,
    ISchemaComparisonService schemaComparison,
    IHttpApiExportService exportService) : ControllerBase
{
    // ── Definitions CRUD ──────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var defs = await store.GetAllDefinitionsAsync(ct);
        var favs = await store.GetFavouriteIdsAsync(ct);
        return Ok(new { definitions = defs, favouriteIds = favs });
    }

    /// <summary>
    /// Returns the most recent invocation status (statusCode + invokedAt) per endpoint,
    /// sourced from history — more up-to-date than the stored lastStatusCode on the definition.
    /// </summary>
    [HttpGet("latest-statuses")]
    public async Task<IActionResult> GetLatestStatuses(CancellationToken ct)
    {
        var latest = await snapshotStore.GetLatestStatusesAsync(ct);
        var result = latest.ToDictionary(
            kvp => kvp.Key,
            kvp => new { statusCode = kvp.Value.StatusCode, invokedAt = kvp.Value.InvokedAt });
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOne(string id, CancellationToken ct)
    {
        var def = await store.GetDefinitionAsync(id, ct);
        if (def is null) return NotFound();
        return Ok(def);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveHttpApiRequest request, CancellationToken ct)
    {
        var all = await store.GetAllDefinitionsAsync(ct);
        if (all.Any(d => string.Equals(d.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"An API definition named '{request.Name}' already exists." });

        var def = MapFromRequest(request);
        var saved = await store.SaveDefinitionAsync(def, ct);
        return CreatedAtAction(nameof(GetOne), new { id = saved.Id }, saved);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] SaveHttpApiRequest request, CancellationToken ct)
    {
        var existing = await store.GetDefinitionAsync(id, ct);
        if (existing is null) return NotFound();

        // Check name collision when renaming
        if (!string.Equals(request.Name, existing.Name, StringComparison.OrdinalIgnoreCase))
        {
            var all = await store.GetAllDefinitionsAsync(ct);
            if (all.Any(d => !string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(d.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
                return Conflict(new { error = $"An API definition named '{request.Name}' already exists." });
        }

        var updated = MapFromRequest(request);
        updated.Id             = existing.Id;
        updated.CreatedAt      = existing.CreatedAt;
        updated.LastInvokedAt  = existing.LastInvokedAt;
        updated.LastStatusCode = existing.LastStatusCode;
        var saved = await store.SaveDefinitionAsync(updated, ct);
        return Ok(saved);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await store.DeleteDefinitionAsync(id, ct);
        return NoContent();
    }

    /// <summary>Duplicate an existing definition with a new name (copy).</summary>
    [HttpPost("{id}/copy")]
    public async Task<IActionResult> Copy(string id, CancellationToken ct)
    {
        var existing = await store.GetDefinitionAsync(id, ct);
        if (existing is null) return NotFound();

        var all = await store.GetAllDefinitionsAsync(ct);

        // Generate a unique name: "Name (copy)", "Name (copy 2)", …
        var baseName = $"{existing.Name} (copy)";
        var newName  = baseName;
        var n = 2;
        while (all.Any(d => string.Equals(d.Name, newName, StringComparison.OrdinalIgnoreCase)))
            newName = $"{baseName} {n++}";

        var copy = CloneDefinition(existing, newName);
        var saved = await store.SaveDefinitionAsync(copy, ct);
        return CreatedAtAction(nameof(GetOne), new { id = saved.Id }, saved);
    }

    // ── Favourites ────────────────────────────────────────────────────────────

    [HttpPatch("{id}/favourite")]
    public async Task<IActionResult> SetFavourite(string id, [FromBody] SetFavouriteRequest request, CancellationToken ct)
    {
        await store.SetFavouriteAsync(id, request.IsFavourite, ct);
        return NoContent();
    }

    // ── Invoke & Bookmark ─────────────────────────────────────────────────────

    [HttpPost("{id}/invoke")]
    public async Task<IActionResult> Invoke(
        string id,
        [FromBody] InvokeHttpApiRequest? request,
        CancellationToken ct)
    {
        var def = await store.GetDefinitionAsync(id, ct);
        if (def is null) return NotFound();

        var resolved = HttpApiTemplateResolver.Apply(def, request?.Inputs);
        var result = await invoker.InvokeAsync(def, request?.Inputs, ct);
        var schema = schemaInference.InferSchema(result.Body);
        var hash   = schemaInference.ComputeSchemaHash(schema);

        // Record invocation in history
        await snapshotStore.AppendInvocationAsync(new HttpApiInvocationRecord
        {
            EndpointId       = def.Id,
            EndpointName     = def.Name,
            StatusCode       = result.StatusCode,
            LatencyMs        = result.LatencyMs,
            SchemaHash       = hash,
            ErrorMessage     = result.ErrorMessage,
            RequestMethod      = resolved.Method,
            RequestBaseUrl     = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.BaseUrl),
            RequestPath        = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.Path),
            RequestHeaders     = HttpApiInvocationSanitizer.SanitizeHeaders(ToSnapshotDictionary(resolved.Headers.Select(h => (h.Name, h.Value)))),
            RequestQueryParams = HttpApiInvocationSanitizer.SanitizeQueryParams(ToSnapshotDictionary(resolved.QueryParams.Where(q => q.Enabled).Select(q => (q.Name, q.Value)))),
            ResponseHeaders    = HttpApiInvocationSanitizer.SanitizeHeaders(result.ResponseHeaders),
            ContentType        = result.ContentType,
            Body               = HttpApiInvocationSanitizer.SanitizeBody(result.TruncatedBody),
            InvokedVia         = HttpApiInvocationSource.App
        }, ct);

        // Update lastInvokedAt and lastStatusCode
        def.LastInvokedAt  = DateTime.UtcNow;
        def.LastStatusCode = result.StatusCode;
        await store.SaveDefinitionAsync(def, ct);

        return Ok(new
        {
            statusCode      = result.StatusCode,
            latencyMs       = result.LatencyMs,
            responseHeaders = result.ResponseHeaders,
            contentType     = result.ContentType,
            body            = result.Body,
            inferredSchema  = schema,
            schemaHash      = hash,
            errorMessage    = result.ErrorMessage
        });
    }

    [HttpPost("{id}/bookmark")]
    public async Task<IActionResult> Bookmark(string id, [FromBody] BookmarkRequest? request, CancellationToken ct)
    {
        var def = await store.GetDefinitionAsync(id, ct);
        if (def is null) return NotFound();

        var resolved = HttpApiTemplateResolver.Apply(def, request?.Inputs);
        var result = await invoker.InvokeAsync(def, request?.Inputs, ct);
        if (!result.IsSuccess && result.StatusCode != 0)
        {
            // Allow bookmarking non-2xx to track error states, but flag it
        }

        var schema = schemaInference.InferSchema(result.Body);

        var snapshot = new HttpResponseSnapshot
        {
            EndpointId         = def.Id,
            EndpointName       = def.Name,
            StatusCode         = result.StatusCode,
            LatencyMs          = result.LatencyMs,
            ResponseHeaders    = result.ResponseHeaders,
            InferredSchema     = schema,
            RawBodyTruncated   = result.TruncatedBody,
            ContentType        = result.ContentType,
            Label              = request?.Label
        };

        await snapshotStore.AppendSnapshotAsync(snapshot, ct);

        await snapshotStore.AppendInvocationAsync(new HttpApiInvocationRecord
        {
            EndpointId         = def.Id,
            EndpointName       = def.Name,
            StatusCode         = result.StatusCode,
            LatencyMs          = result.LatencyMs,
            SchemaHash         = schemaInference.ComputeSchemaHash(schema),
            RequestMethod      = resolved.Method,
            RequestBaseUrl     = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.BaseUrl),
            RequestPath        = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.Path),
            RequestHeaders     = HttpApiInvocationSanitizer.SanitizeHeaders(ToSnapshotDictionary(resolved.Headers.Select(h => (h.Name, h.Value)))),
            RequestQueryParams = HttpApiInvocationSanitizer.SanitizeQueryParams(ToSnapshotDictionary(resolved.QueryParams.Where(q => q.Enabled).Select(q => (q.Name, q.Value)))),
            ResponseHeaders    = HttpApiInvocationSanitizer.SanitizeHeaders(result.ResponseHeaders),
            ContentType        = result.ContentType,
            Body               = HttpApiInvocationSanitizer.SanitizeBody(result.TruncatedBody),
            InvokedVia         = HttpApiInvocationSource.App
        }, ct);

        return Ok(snapshot);
    }

    [HttpPost("{id}/compare")]
    public async Task<IActionResult> Compare(
        string id,
        [FromBody] CompareHttpApiRequest? request,
        [FromQuery] string? snapshotId,
        CancellationToken ct)
    {
        var def = await store.GetDefinitionAsync(id, ct);
        if (def is null) return NotFound();

        // Resolve baseline: pinned golden snapshot → specified snapshot → latest
        var targetSnapshotId = snapshotId ?? def.GoldenSnapshotId;
        HttpResponseSnapshot? baseline;

        if (!string.IsNullOrEmpty(targetSnapshotId))
            baseline = await snapshotStore.GetSnapshotAsync(id, targetSnapshotId, ct);
        else
        {
            var all = await snapshotStore.GetSnapshotsAsync(id, ct);
            baseline = all.MaxBy(s => s.CapturedAt);
        }

        if (baseline is null)
            return BadRequest(new { error = "No baseline snapshot found. Invoke /bookmark first." });

        var result = await invoker.InvokeAsync(def, request?.Inputs, ct);
        var resolved = HttpApiTemplateResolver.Apply(def, request?.Inputs);
        var schema = schemaInference.InferSchema(result.Body);
        var hash   = schemaInference.ComputeSchemaHash(schema);

        var comparison = schemaComparison.Compare(def, baseline, result.StatusCode, result.LatencyMs, schema);

        await snapshotStore.AppendInvocationAsync(new HttpApiInvocationRecord
        {
            EndpointId            = def.Id,
            EndpointName          = def.Name,
            StatusCode            = result.StatusCode,
            LatencyMs             = result.LatencyMs,
            SchemaHash            = hash,
            SchemaMatchedSnapshot = !comparison.IsBreaking,
            ErrorMessage = result.ErrorMessage,
            RequestMethod         = resolved.Method,
            RequestBaseUrl        = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.BaseUrl),
            RequestPath           = HttpApiInvocationSanitizer.SanitizeUrlComponent(resolved.Path),
            RequestHeaders        = HttpApiInvocationSanitizer.SanitizeHeaders(ToSnapshotDictionary(resolved.Headers.Select(h => (h.Name, h.Value)))),
            RequestQueryParams    = HttpApiInvocationSanitizer.SanitizeQueryParams(ToSnapshotDictionary(resolved.QueryParams.Where(q => q.Enabled).Select(q => (q.Name, q.Value)))),
            ResponseHeaders       = HttpApiInvocationSanitizer.SanitizeHeaders(result.ResponseHeaders),
            ContentType           = result.ContentType,
            Body                  = HttpApiInvocationSanitizer.SanitizeBody(result.TruncatedBody),
            InvokedVia            = HttpApiInvocationSource.App
        }, ct);

        return Ok(new
        {
            comparison,
            liveResponse = new
            {
                statusCode     = result.StatusCode,
                latencyMs      = result.LatencyMs,
                inferredSchema = schema,
                body           = result.Body,
                contentType    = result.ContentType
            }
        });
    }

    // ── Snapshots ─────────────────────────────────────────────────────────────

    [HttpGet("{id}/snapshots")]
    public async Task<IActionResult> GetSnapshots(string id, CancellationToken ct)
    {
        var snapshots = await snapshotStore.GetSnapshotsAsync(id, ct);
        return Ok(snapshots.OrderByDescending(s => s.CapturedAt));
    }

    [HttpDelete("{id}/snapshots/{snapshotId}")]
    public async Task<IActionResult> DeleteSnapshot(string id, string snapshotId, CancellationToken ct)
    {
        await snapshotStore.DeleteSnapshotAsync(id, snapshotId, ct);
        return NoContent();
    }

    [HttpPatch("{id}/snapshots/{snapshotId}/pin")]
    public async Task<IActionResult> PinSnapshot(string id, string snapshotId, CancellationToken ct)
    {
        var def = await store.GetDefinitionAsync(id, ct);
        if (def is null) return NotFound();

        def.GoldenSnapshotId = snapshotId;
        await store.SaveDefinitionAsync(def, ct);
        return NoContent();
    }

    // ── Invocation history ────────────────────────────────────────────────────

    [HttpGet("{id}/history")]
    public async Task<IActionResult> GetHistory(string id, [FromQuery] int? limit, CancellationToken ct)
    {
        var history = await snapshotStore.GetHistoryAsync(id, limit, ct);
        return Ok(history);
    }

    // ── Groups ────────────────────────────────────────────────────────────────

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups(CancellationToken ct)
    {
        var groups = await store.GetAllGroupsAsync(ct);
        return Ok(groups.OrderBy(g => g.Name));
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] HttpApiGroup group, CancellationToken ct)
    {
        var existing = await store.GetAllGroupsAsync(ct);
        if (existing.Any(g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A group named '{group.Name}' already exists." });

        var saved = await store.SaveGroupAsync(group, ct);
        return CreatedAtAction(nameof(GetGroups), new { }, saved);
    }

    [HttpPut("groups/{name}")]
    public async Task<IActionResult> UpdateGroup(string name, [FromBody] HttpApiGroup group, CancellationToken ct)
    {
        var existing = await store.GetAllGroupsAsync(ct);
        if (!existing.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
            return NotFound();

        if (!string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase) &&
            existing.Any(g => string.Equals(g.Name, group.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A group named '{group.Name}' already exists." });

        var saved = await store.SaveGroupAsync(group, ct);
        return Ok(saved);
    }

    [HttpDelete("groups/{name}")]
    public async Task<IActionResult> DeleteGroup(string name, CancellationToken ct)
    {
        await store.DeleteGroupAsync(name, ct);
        return NoContent();
    }

    // ── Export / Import ───────────────────────────────────────────────────────

    [HttpPost("export")]
    public async Task<IActionResult> Export([FromBody] ExportHttpApisRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (request.Ids is null || request.Ids.Count == 0)
            return BadRequest(new { error = "Select at least one definition to export." });

        var all = await store.GetAllDefinitionsAsync(ct);
        var selected = all
            .Where(d => request.Ids.Any(i => string.Equals(i, d.Id, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (selected.Count == 0)
            return NotFound(new { error = "None of the requested definitions were found." });

        var payload = exportService.Encrypt(selected, request.Password);
        var json    = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return File(bytes, "application/json", "http-apis-export.json");
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportHttpApisRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (request.Payload is null)
            return BadRequest(new { error = "Export payload is required." });

        var payload = new Core.Interfaces.HttpApiExportPayload
        {
            Version = request.Payload.Version,
            Salt    = request.Payload.Salt,
            Nonce   = request.Payload.Nonce,
            Data    = request.Payload.Data
        };

        IReadOnlyList<HttpApiDefinition> decrypted;
        try { decrypted = exportService.Decrypt(payload, request.Password); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }

        var all = await store.GetAllDefinitionsAsync(ct);
        var existingNames = all.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        foreach (var def in decrypted)
        {
            if (string.IsNullOrWhiteSpace(def.Name)) continue;

            var finalName = def.Name;
            if (existingNames.Contains(finalName))
            {
                var v = 2;
                while (existingNames.Contains($"{def.Name} (v{v})")) v++;
                finalName = $"{def.Name} (v{v})";
            }

            def.Id        = Guid.NewGuid().ToString();
            def.Name      = finalName;
            def.CreatedAt = DateTime.UtcNow;
            def.LastUpdatedAt = null;

            await store.SaveDefinitionAsync(def, ct);
            existingNames.Add(finalName);
            imported++;
        }

        return Ok(new { imported, total = decrypted.Count });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpApiDefinition MapFromRequest(SaveHttpApiRequest r) => new()
    {
        Name               = r.Name,
        BaseUrl            = r.BaseUrl,
        Method             = r.Method,
        Path               = r.Path,
        AuthenticationMode = r.AuthenticationMode,
        Headers            = r.Headers ?? [],
        QueryParams        = r.QueryParams ?? [],
        BodyTemplate       = r.BodyTemplate,
        GroupName          = r.GroupName,
        Tags               = r.Tags ?? [],
        Note               = r.Note ?? string.Empty,
        AzureCredentials   = r.AzureCredentials,
        ApiKeyOptions      = r.ApiKeyOptions,
        BearerOptions      = r.BearerOptions
    };

    private static HttpApiDefinition CloneDefinition(HttpApiDefinition src, string newName) => new()
    {
        Id                 = Guid.NewGuid().ToString(),
        Name               = newName,
        BaseUrl            = src.BaseUrl,
        Method             = src.Method,
        Path               = src.Path,
        AuthenticationMode = src.AuthenticationMode,
        Headers            = [..src.Headers.Select(h => new HttpApiHeader { Name = h.Name, Value = h.Value })],
        QueryParams        = [..src.QueryParams.Select(q => new HttpApiQueryParam { Name = q.Name, Value = q.Value, Enabled = q.Enabled })],
        BodyTemplate       = src.BodyTemplate,
        GroupName          = src.GroupName,
        Tags               = [..src.Tags],
        Note               = src.Note,
        AzureCredentials   = src.AzureCredentials is null ? null : new HttpApiAzureCredentialsOptions
        {
            TenantId      = src.AzureCredentials.TenantId,
            ClientId      = src.AzureCredentials.ClientId,
            ClientSecret  = src.AzureCredentials.ClientSecret,
            Scope         = src.AzureCredentials.Scope,
            AuthorityHost = src.AzureCredentials.AuthorityHost,
            KeyVaultSecretRef = src.AzureCredentials.KeyVaultSecretRef,
            SubscriptionId = src.AzureCredentials.SubscriptionId
        },
        ApiKeyOptions      = src.ApiKeyOptions is null ? null : new HttpApiApiKeyOptions
        {
            HeaderName = src.ApiKeyOptions.HeaderName,
            ApiKey     = src.ApiKeyOptions.ApiKey,
            Prefix     = src.ApiKeyOptions.Prefix
        },
        BearerOptions      = src.BearerOptions is null ? null : new HttpApiBearerOptions { Token = src.BearerOptions.Token },
        CreatedAt          = DateTime.UtcNow
    };

    private static Dictionary<string, string> ToSnapshotDictionary(IEnumerable<(string Name, string Value)> items)
    {
        return items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
    }
}
