using System.ComponentModel;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class CertRenewCommand : AsyncCommand<CertRenewCommand.Settings>
{
    private readonly ICertificateRenewalService _renewalService;

    public CertRenewCommand(ICertificateRenewalService renewalService) => _renewalService = renewalService;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--name")] [Description("Certificate name to renew")] public string Name { get; init; } = string.Empty;
        [CommandOption("--remove-old")] [Description("Remove the old key credential(s) from Azure after the upload succeeds")] public bool RemoveOld { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = await _renewalService.RenewAsync(settings.Name, settings.RemoveOld, cancellationToken);
        return CertOutput.RenderResult(result);
    }
}
