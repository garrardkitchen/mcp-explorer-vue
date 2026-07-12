using System.ComponentModel;
using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Garrard.Mcp.Explorer.Core.Interfaces;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Garrard.Mcp.Explorer.Cli.Commands;

public sealed class CertDeleteCommand : AsyncCommand<CertDeleteCommand.Settings>
{
    private readonly ICertificateService _certificates;

    public CertDeleteCommand(ICertificateService certificates) => _certificates = certificates;

    public sealed class Settings : CommandSettings
    {
        [CommandOption("-n|--name")] [Description("Certificate name to delete")] public string Name { get; init; } = string.Empty;
        [CommandOption("-y|--yes")] [Description("Skip the confirmation prompt")] public bool Yes { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!settings.Yes && !Console.IsInputRedirected)
        {
            var confirmed = AnsiConsole.Prompt(new ConfirmationPrompt(
                $"Delete certificate [bold]{Markup.Escape(settings.Name)}[/]? This removes its private key from disk."));
            if (!confirmed) return 1;
        }

        try
        {
            await _certificates.DeleteAsync(settings.Name);
            AnsiConsole.MarkupLine($"[green]✓ Deleted {Markup.Escape(settings.Name)}[/]");
            return 0;
        }
        catch (CertificateInUseException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(settings.Name)} is still in use — detach it first:[/]");
            foreach (var name in ex.Usage.ConnectionNames)
                AnsiConsole.MarkupLine($"  [yellow]connection[/] {Markup.Escape(name)}");
            foreach (var name in ex.Usage.HttpApiNames)
                AnsiConsole.MarkupLine($"  [yellow]http api[/] {Markup.Escape(name)}");
            return 1;
        }
        catch (FileNotFoundException)
        {
            AnsiConsole.MarkupLine($"[red]✗ Certificate '{Markup.Escape(settings.Name)}' does not exist.[/]");
            return 1;
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ {Markup.Escape(ex.Message)}[/]");
            return 1;
        }
    }
}
