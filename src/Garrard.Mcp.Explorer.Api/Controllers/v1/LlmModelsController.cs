using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.LlmModels;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/llm-models")]
public sealed class LlmModelsController(IUserPreferencesStore store, IAiChatService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var p = await store.LoadAsync(ct);
        return Ok(p.LlmModels);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] LlmModelDefinition model, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        var updated = prefs with { LlmModels = [..prefs.LlmModels, model] };
        await store.SaveAsync(updated, ct);
        return CreatedAtAction(nameof(GetAll), new { }, model);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] LlmModelDefinition model, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        if (!prefs.LlmModels.Any(m => m.Name == name))
            return NotFound();

        // Collision check when renaming
        if (!string.Equals(model.Name, name, StringComparison.OrdinalIgnoreCase) &&
            prefs.LlmModels.Any(m => string.Equals(m.Name, model.Name, StringComparison.OrdinalIgnoreCase)))
            return Conflict(new { error = $"A model named '{model.Name}' already exists." });

        var models = prefs.LlmModels.Where(m => m.Name != name).ToList();
        models.Add(model);
        await store.SaveAsync(prefs with { LlmModels = models }, ct);
        return Ok(model);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        await store.SaveAsync(prefs with { LlmModels = prefs.LlmModels.Where(m => m.Name != name).ToList() }, ct);
        return NoContent();
    }

    [HttpPatch("{name}/system-prompt")]
    public async Task<IActionResult> UpdateSystemPrompt(string name, [FromBody] UpdateSystemPromptRequest request, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        var model = prefs.LlmModels.FirstOrDefault(m => m.Name == name);
        if (model is null) return NotFound();
        model.SystemPrompt = request.SystemPrompt ?? string.Empty;
        await store.SaveAsync(prefs, ct);
        return Ok(new { systemPrompt = model.SystemPrompt });
    }

    [HttpPost("{name}/test")]
    public async Task<IActionResult> Test(string name, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        var model = prefs.LlmModels.FirstOrDefault(m =>
            string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
        if (model is null)
            return NotFound();

        try
        {
            var output = await chatService.TestAsync(model, ct);
            var summary = output.Length <= 500 ? output : $"{output[..500]}…";
            return Ok(new { success = true, message = summary });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Preserve the SDK's top-level context. GetBaseException() can reduce a
            // credential-chain failure to a misleading low-level IMDS socket error.
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("selected")]
    public async Task<IActionResult> GetSelected(CancellationToken ct)
    {
        var p = await store.LoadAsync(ct);
        return Ok(new { selectedModelName = p.SelectedLlmModelName });
    }

    [HttpPut("selected")]
    public async Task<IActionResult> SetSelected([FromBody] SetSelectedModelRequest request, CancellationToken ct)
    {
        var prefs = await store.LoadAsync(ct);
        await store.SaveAsync(prefs with { SelectedLlmModelName = request.ModelName }, ct);
        return Ok();
    }
}

public sealed record SetSelectedModelRequest(string ModelName);
public sealed record UpdateSystemPromptRequest(string? SystemPrompt);
