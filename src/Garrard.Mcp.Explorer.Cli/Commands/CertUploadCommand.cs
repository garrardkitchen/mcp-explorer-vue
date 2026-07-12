using System.ComponentModel;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class CertUploadCommand : AsyncCommand<CertUploadCommand.Settings>
{
    private readonly ICertificateUploadService _uploadService;

    public CertUploadCommand(ICertificateUploadService uploadService) => _uploadService = uploadService;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--name")] [Description("Certificate name in the local store")] public string Name { get; init; } = string.Empty;
        [CommandOption("--app-id")] [Description("App registration client id (GUID)")] public string AppId { get; init; } = string.Empty;
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = await _uploadService.UploadAsync(settings.Name, settings.AppId, cancellationToken);
        return CertOutput.RenderResult(result);
    }
}
