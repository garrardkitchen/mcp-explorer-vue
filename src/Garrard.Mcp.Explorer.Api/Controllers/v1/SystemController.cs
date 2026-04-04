using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SystemController : ControllerBase
{
    [HttpGet("info")]
    public IActionResult GetInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var apiVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";

        // Strip any build metadata suffix (e.g. "+abc123" from SourceLink)
        var plusIdx = apiVersion.IndexOf('+');
        if (plusIdx > 0) apiVersion = apiVersion[..plusIdx];

        return Ok(new
        {
            ApiVersion = apiVersion,
            DotnetVersion = RuntimeInformation.FrameworkDescription,
        });
    }
}
