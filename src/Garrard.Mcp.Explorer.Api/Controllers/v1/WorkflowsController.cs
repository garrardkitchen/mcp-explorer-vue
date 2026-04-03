using Asp.Versioning;
using Garrard.Mcp.Explorer.Core.Domain.Workflows;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Garrard.Mcp.Explorer.Infrastructure.Workflows;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workflows")]
public sealed class WorkflowsController(IWorkflowService workflowService, LoadTestService loadTestService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await workflowService.GetAllAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] WorkflowDefinition workflow, CancellationToken ct)
        => Ok(await workflowService.CreateAsync(workflow, ct));

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] WorkflowDefinition workflow, CancellationToken ct)
        => Ok(await workflowService.UpdateAsync(workflow, ct));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await workflowService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id}/history")]
    public async Task<IActionResult> History(string id, CancellationToken ct)
        => Ok(await workflowService.GetExecutionHistoryAsync(id, cancellationToken: ct));

    [HttpPost("{id}/execute")]
    public async Task<IActionResult> Execute(string id, [FromBody] ExecuteWorkflowRequest request, CancellationToken ct)
        => Ok(await workflowService.ExecuteAsync(id, request.ConnectionName, request.RuntimeParameters, cancellationToken: ct));

    [HttpPost("{id}/load-test")]
    public async Task<IActionResult> LoadTest(string id, [FromBody] LoadTestRequest request, CancellationToken ct)
    {
        var wf = await workflowService.GetByIdAsync(id, ct);
        if (wf is null) return NotFound();
        var runId = loadTestService.StartAsync(id, request.ConnectionName, request.DurationSeconds, request.MaxParallel, wf.Name, request.RuntimeParameters, ct);
        return Ok(new { runId });
    }

    [HttpGet("load-test-progress/{runId}")]
    public IActionResult LoadTestProgress(string runId)
    {
        var progress = loadTestService.GetProgress(runId);
        if (progress is null) return NotFound();
        return Ok(progress);
    }

    [HttpGet("{id}/load-test-history")]
    public async Task<IActionResult> LoadTestHistory(string id, CancellationToken ct)
        => Ok(await loadTestService.ListResultsAsync(id, ct));

    [HttpGet("export/{id}")]
    public async Task<IActionResult> Export(string id, CancellationToken ct)
    {
        var wf = await workflowService.GetByIdAsync(id, ct);
        if (wf is null) return NotFound();
        return Ok(workflowService.ExportToJson(wf));
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] JsonElement payload, CancellationToken ct)
    {
        var wf = workflowService.ImportFromJson(payload.GetRawText());
        if (wf is null) return BadRequest("Invalid workflow JSON.");
        return Ok(await workflowService.CreateAsync(wf, ct));
    }
}

public sealed record ExecuteWorkflowRequest(string ConnectionName, Dictionary<string, string>? RuntimeParameters);
public sealed record LoadTestRequest(string ConnectionName, int DurationSeconds, int MaxParallel, Dictionary<string, string>? RuntimeParameters);
