using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class CertListCommand : AsyncCommand
{
    private readonly ICertificateService _certificates;

    public CertListCommand(ICertificateService certificates) => _certificates = certificates;

    protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var certs = await _certificates.ListAsync();
        if (certs.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No certificates in the local store.[/]");
            AnsiConsole.MarkupLine($"[grey]Store: {Markup.Escape(_certificates.CertificatesDirectory)}[/]");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Name");
        table.AddColumn("Subject");
        table.AddColumn("State");
        table.AddColumn("Expires");
        table.AddColumn("Thumbprint (SHA-1)");
        table.AddColumn("Uploaded To");

        foreach (var cert in certs)
        {
            var usage = await _certificates.GetUsageAsync(cert.Name);
            var stateMarkup = cert.State switch
            {
                Core.Domain.Certificates.CertificateState.Active => "[green]Active[/]",
                Core.Domain.Certificates.CertificateState.CsrPending => "[blue]CSR pending[/]",
                _ => $"[grey]Superseded → {Markup.Escape(cert.RenewedBy ?? "?")}[/]",
            };
            var uploadedTo = cert.UploadedTo.Count == 0
                ? "[grey]—[/]"
                : Markup.Escape(string.Join(", ", cert.UploadedTo.Select(u => u.DisplayName)));
            var name = usage.IsInUse
                ? $"[bold]{Markup.Escape(cert.Name)}[/] [grey](in use)[/]"
                : $"[bold]{Markup.Escape(cert.Name)}[/]";

            table.AddRow(
                name,
                Markup.Escape(cert.Subject),
                stateMarkup,
                CertOutput.ExpiryMarkup(cert),
                Markup.Escape(cert.ThumbprintSha1 is { Length: > 8 } t ? $"{t[..8]}…" : cert.ThumbprintSha1 ?? "—"),
                uploadedTo);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"[grey]Store: {Markup.Escape(_certificates.CertificatesDirectory)}[/]");
        return 0;
    }
}
