#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

public sealed class McpConceptDocumentTests
{
    private const string KonzeptRelativerPfad = "tasks/codegraph-mcp-server/konzept.md";

    private static string ReadKonzeptText()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, KonzeptRelativerPfad)))
        {
            dir = dir.Parent;
        }
        Assert.NotNull(dir);
        return File.ReadAllText(Path.Combine(dir!.FullName, KonzeptRelativerPfad));
    }

    private static string ExtractToolTableRow(string konzept, string toolName)
    {
        var line = konzept.Split('\n').FirstOrDefault(l =>
            l.TrimStart().StartsWith($"| `{toolName}` |"));
        Assert.NotNull(line);
        return line!;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetViolations_StatusIstFertig()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_violations");
        Assert.Contains("| fertig |", row);
        Assert.DoesNotContain("Review offen", row);
        Assert.DoesNotContain("| offen |", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetViolations_InputBeschreibtScopeFilter()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_violations");
        Assert.Contains("scopeFilter", row);
        Assert.DoesNotContain("Datei-/Symbol-Scope", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_SearchPattern_StatusIstFertig()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "search_pattern");
        Assert.Contains("| fertig |", row);
        Assert.DoesNotContain("| offen |", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_GetImpact_InputBeschreibtExklusiveParameter()
    {
        var row = ExtractToolTableRow(ReadKonzeptText(), "get_impact");
        Assert.Contains("gitRef", row);
        Assert.Contains("symbolIdentifier", row);
        Assert.Contains("exklusiv", row);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Konzept_ServerBetrieb_KaltstartAlsSollFormuliert()
    {
        var konzept = ReadKonzeptText();
        // Konjunktiv ("sollen") und Verweis auf den P0/P1-Rest "Kaltstart entkoppeln"
        // muessen vorhanden sein; der irrefuehrende Indikativ ("stehen ... sofort
        // bereit") muss weg sein. Whitespace wird per Regex toleriert, weil die
        // Formulierung aktuell ueber zwei Markdown-Zeilen verteilt ist (Z. 559
        // endet mit "unabhaengig vom", Z. 560 beginnt mit "   Ladezustand")
        // und beim Re-Wrap beliebig umbrechen kann. Plan-Abweichung
        Assert.Contains("**sollen**", konzept);
        Assert.Matches(new System.Text.RegularExpressions.Regex(
            @"\*\*sollen\*\*\s*unabhängig\s+vom\s+Ladezustand"), konzept);
        Assert.Contains("Kaltstart entkoppeln", konzept);
        Assert.DoesNotContain("stehen dabei unabhängig vom Ladezustand sofort bereit", konzept);
    }
}
