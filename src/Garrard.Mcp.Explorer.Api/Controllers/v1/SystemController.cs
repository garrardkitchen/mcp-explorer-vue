using System.Reflection;
using System.Runtime.InteropServices;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Garrard.Mcp.Explorer.Api.Controllers.v1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public sealed class SystemController(IConfiguration configuration) : ControllerBase
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

        var storagePath = configuration["PREFERENCES:StoragePath"]
            ?? Environment.GetEnvironmentVariable("PREFERENCES__StoragePath");

        var dataPath = string.IsNullOrWhiteSpace(storagePath)
            ? OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpExplorer")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "McpExplorer")
            : Path.GetDirectoryName(storagePath) ?? string.Empty;

        return Ok(new
        {
            ApiVersion = apiVersion,
            DotnetVersion = RuntimeInformation.FrameworkDescription,
            DataPath = dataPath,
        });
    }
}
