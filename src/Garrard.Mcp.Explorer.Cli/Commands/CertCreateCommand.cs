using System.ComponentModel;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class CertCreateCommand : AsyncCommand<CertCreateCommand.Settings>
{
    private readonly ICertificateService _certificates;

    public CertCreateCommand(ICertificateService certificates) => _certificates = certificates;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--name")] [Description("Certificate name (lowercase letters, digits, hyphens)")] public string Name { get; init; } = string.Empty;
        [CommandOption("--subject")] [Description("Subject CN (default: mcp-explorer-{name})")] public string? SubjectCn { get; init; }
        [CommandOption("--key-size")] [Description("RSA key size: 2048 or 4096 (default: 2048)")] public int KeySize { get; init; } = 2048;
        [CommandOption("--validity")] [Description("Validity in months: 6, 12, or 24 (default: 12)")] public int ValidityMonths { get; init; } = 12;
        [CommandOption("--pfx-password")] [Description("Also write a password-protected cert.pfx")] public string? PfxPassword { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var result = await _certificates.GenerateSelfSignedAsync(new GenerateCertificateRequest
        {
            Name = settings.Name,
            SubjectCn = settings.SubjectCn,
            KeySize = settings.KeySize,
            ValidityMonths = settings.ValidityMonths,
            PfxPassword = settings.PfxPassword,
        });

        return CertOutput.RenderResult(result);
    }
}
