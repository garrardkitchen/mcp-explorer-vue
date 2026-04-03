using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.Preferences;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/preferences")]
public sealed class PreferencesController(IUserPreferencesStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await store.LoadAsync(ct));

    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme(CancellationToken ct)
    {
        var p = await store.LoadAsync(ct);
        return Ok(new { theme = p.Theme });
    }

    [HttpPut("theme")]
    public async Task<IActionResult> SetTheme([FromBody] UpdateThemeRequest request, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        await store.SaveAsync(prefs with { Theme = request.Theme }, ct);
        return Ok(new { theme = request.Theme });
    }

    [HttpGet("sensitive-fields")]
    public async Task<IActionResult> GetSensitiveFields(CancellationToken ct)
    {
        var p = await store.LoadAsync(ct);
        return Ok(p.SensitiveFieldConfig);
    }

    [HttpPut("sensitive-fields")]
    public async Task<IActionResult> SetSensitiveFields([FromBody] SensitiveFieldConfiguration config, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        await store.SaveAsync(prefs with { SensitiveFieldConfig = config }, ct);
        return Ok(config);
    }

    [HttpPatch]
    public async Task<IActionResult> Patch([FromBody] PatchPreferencesRequest request, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        var updated = prefs with
        {
            FavoriteTools = request.FavoriteTools ?? prefs.FavoriteTools,
            FavoriteConnections = request.FavoriteConnections ?? prefs.FavoriteConnections,
            FavoritePrompts = request.FavoritePrompts ?? prefs.FavoritePrompts,
            FavoriteResources = request.FavoriteResources ?? prefs.FavoriteResources,
            ShowFavoritesFirst = request.ShowFavoritesFirst ?? prefs.ShowFavoritesFirst,
            ParameterHistory = request.ParameterHistory ?? prefs.ParameterHistory,
        };
        await store.SaveAsync(updated, ct);
        return Ok(updated);
    }
}

public sealed record UpdateThemeRequest([property: Required, MinLength(1)] string Theme);

public sealed record PatchPreferencesRequest(
    List<string>? FavoriteTools,
    List<string>? FavoriteConnections,
    List<string>? FavoritePrompts,
    List<string>? FavoriteResources,
    bool? ShowFavoritesFirst,
    Dictionary<string, List<string>>? ParameterHistory
);
