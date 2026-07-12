using Garrard.Mcp.Explorer.Core.Domain.Certificates;
using Spectre.Console;

namespace Garrard.Mcp.Explorer.Cli.Commands;

/// <summary>Shared Spectre rendering for certificate operation step results.</summary>
internal static class CertOutput
{
    public static int RenderResult(OperationResult result)
    {
        foreach (var step in result.Steps)
        {
            var (glyph, colour) = step.Status switch
            {
                StepStatus.Succeeded => ("✓", "green"),
                StepStatus.Failed => ("✗", "red"),
                _ => ("→", "grey"),
            };

            AnsiConsole.MarkupLine($"[{colour}]{glyph}[/] {Markup.Escape(step.Label)}");
            if (!string.IsNullOrWhiteSpace(step.Message))
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(step.Message)}[/]");
        }

        if (result.Success && result.Certificate is { } cert)
        {
            AnsiConsole.MarkupLine(
                $"\n[green]✓[/] [bold]{Markup.Escape(cert.Name)}[/] " +
                $"[grey]({Markup.Escape(cert.Subject)}; expires {cert.NotAfter:yyyy-MM-dd}; thumbprint {Markup.Escape(cert.ThumbprintSha1 ?? "n/a")})[/]");
        }

        return result.Success ? 0 : 1;
    }

    public static string ExpiryMarkup(CertificateInfo cert)
    {
        if (cert.NotAfter is not { } notAfter) return "[grey]—[/]";
        var days = (int)Math.Ceiling((notAfter - DateTimeOffset.UtcNow).TotalDays);
        return days switch
        {
            <= 0 => $"[red]{notAfter:yyyy-MM-dd} (expired)[/]",
            <= 30 => $"[yellow]{notAfter:yyyy-MM-dd} ({days}d)[/]",
            _ => $"[green]{notAfter:yyyy-MM-dd} ({days}d)[/]",
        };
    }
}
