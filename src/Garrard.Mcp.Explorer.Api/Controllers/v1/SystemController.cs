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

        // HOST_DATA_PATH is injected by Docker (run.sh / docker-compose) so the
        // frontend can build a CLI --data-path that works on the host, not inside
        // the container. Three cases:
        //   null  → env var not set (native run) → derive from config
        //   ""    → injected by Docker but MCP_DATA_PATH was not set → return null
        //           (no named volume has a usable host path; user must supply path)
        //   path  → Docker with MCP_DATA_PATH set → return the host path
        var rawHostPath = Environment.GetEnvironmentVariable("HOST_DATA_PATH");

        string? dataPath;
        if (rawHostPath != null)
        {
            // Running in a Docker context; use the host path or null if unknown.
            dataPath = string.IsNullOrWhiteSpace(rawHostPath) ? null : rawHostPath;
        }
        else
        {
            var storagePath = configuration["PREFERENCES:StoragePath"]
                ?? Environment.GetEnvironmentVariable("PREFERENCES__StoragePath");

            dataPath = string.IsNullOrWhiteSpace(storagePath)
                ? OperatingSystem.IsWindows()
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "McpExplorer")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "McpExplorer")
                : Path.GetDirectoryName(storagePath) ?? string.Empty;
        }

        return Ok(new
        {
            ApiVersion = apiVersion,
            DotnetVersion = RuntimeInformation.FrameworkDescription,
            DataPath = dataPath,
        });
    }
}
