using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Models;
using AiNetLinter.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.Tests.Mcp.Tools;

[Trait("Category", "Unit")]
[Collection("SymbolGraphCatalog")]
public sealed class GetViolationsToolTests
{
    private readonly SymbolGraphCatalogFixture _fixture;

    public GetViolationsToolTests(SymbolGraphCatalogFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await GetViolationsTool.ExecuteAsync(state, null, GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolutionNoScopeFilter_ReturnsViolationForKnownFixture()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, null, GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Lint-Violations:", textContent.Text, StringComparison.Ordinal);
        // Sufficiency-Hinweis: get_violations liefert immer den vollstaendigen Report fuer den Scope.
        Assert.Contains("vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_RestrictsViolations()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ViolationTrigger", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_StructuredContentDeserializesToRuleViolations()
    {
        // Structured-Output-Mode: StructuredContent ergaenzt den Text additiv, ohne ihn zu
        // aendern (siehe die unveraenderten Text-Assertions in den anderen Tests dieser Klasse).
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        Assert.NotNull(result.StructuredContent);
        var violations = result.StructuredContent!.Value.GetProperty("violations")
            .Deserialize<List<RuleViolation>>(McpJsonOptions.Default);
        Assert.NotNull(violations);
        Assert.NotEmpty(violations!);
        Assert.Contains(violations!, v => v.RuleName is not null && v.FilePath.Contains("ViolationTrigger", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesNoFile_ReturnsExplicitNoScopeMessage()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, "DoesNotExistAnywhere", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("| Datei | Zeile | Regel | Details |", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_DoesNotIncludeCompileErrorsAsViolations()
    {
        using var fixture = new CompileErrorMiniFixtureWorkspace();
        var catalog = await SourceFileCatalog.LoadAsync(fixture.RootPath);
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, null, GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("CS1513", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CS0246", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Hinweis:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LinterEngineThrows_ReturnsMalfunctionWithIsErrorTrueAndRetryHint()
    {
        // Regressionstest fuer den Fix in GetViolationsScanner/GetViolationsTool: vor diesem
        // Epic lief eine echte LinterEngine-Malfunction (unerwartete Exception) unmarkiert als
        // Erfolg durch McpToolResults.Text(...) — IsError blieb faelschlich false. Simuliert
        // wird eine realistische Malfunction (Quelldatei zwischen Indexierung und Analyse vom
        // Dateisystem verschwunden -> IOException beim Text-Zugriff) ueber einen deterministischen
        // TextLoader-Fake, statt auf einen fragilen realen Timing-Race zu warten.
        var probeDir = Path.Combine(Path.GetTempPath(), "ainetlinter-malfunction-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(probeDir);
        var faultyPath = Path.Combine(probeDir, "Faulty.cs");
        try
        {
            // Die Datei muss real auf der Platte existieren, sonst entfernt
            // McpCodeGraphServerRefresh.RemoveDeletedDocuments sie beim GetCurrentSolution()-Aufruf
            // schon vor der Analyse (File.Exists-Check) — der reale Dateiinhalt ist irrelevant,
            // weil der untenstehende ThrowingTextLoader den Text-Zugriff uebernimmt, nicht Roslyns
            // Standard-Dateisystem-Loader.
            File.WriteAllText(faultyPath, "class Faulty {}");

            var workspace = new AdhocWorkspace();
            var projectId = ProjectId.CreateNewId();
            var projectInfo = ProjectInfo.Create(
                projectId, VersionStamp.Create(), "FaultyProject", "FaultyProject", LanguageNames.CSharp);
            var solution = workspace.CurrentSolution.AddProject(projectInfo);

            var documentId = DocumentId.CreateNewId(projectId);
            var documentInfo = DocumentInfo.Create(
                documentId, "Faulty.cs", filePath: faultyPath, loader: new ThrowingTextLoader());
            solution = solution.AddDocument(documentInfo);

            var catalog = new SourceFileCatalog(solution, hasLoadingErrors: false);
            using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(catalog)));

            var result = await GetViolationsTool.ExecuteAsync(state, null, GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

            Assert.True(result.IsError);
            var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
            Assert.Contains("ANALYSIS_FAILED", textContent.Text, StringComparison.Ordinal);
            Assert.Contains("Einmal erneut versuchen", textContent.Text, StringComparison.Ordinal);
            // Context-Feld (rohe Exception-Message) landet im Text — Nachweis fuer den context:-Fix.
            Assert.Contains("Simulierter Lesefehler", textContent.Text, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(probeDir, recursive: true);
        }
    }

    /// <summary>
    /// Test-Fake: wirft beim Textzugriff eine IOException, um eine echte LinterEngine-Malfunction
    /// deterministisch zu simulieren (statt auf einen fragilen realen Race zu warten, in dem eine
    /// Quelldatei zwischen Indexierung und Analyse vom Dateisystem verschwindet).
    /// </summary>
    private sealed class ThrowingTextLoader : TextLoader
    {
        public override Task<TextAndVersion> LoadTextAndVersionAsync(
            LoadTextOptions options, CancellationToken cancellationToken)
        {
            // Bewusst kein IOException/UnauthorizedAccessException: Roslyns TextDocumentState
            // faengt diese beiden Typen intern ab (Workspace-Resilienz gegen verschwundene
            // Quelldateien) und ersetzt sie durch leeren Text statt die Exception zu propagieren
            // — das wuerde diesen Test zum Erfolgsfall statt zur Malfunction machen (empirisch
            // verifiziert). Ein unspezifischer Exception-Typ hat keine solche Sonderbehandlung.
            throw new InvalidOperationException("Simulierter Lesefehler fuer Malfunction-Regressionstest.");
        }
    }

    [Fact]
    public void FormatReport_ViolationCountExceedsMaxResults_TruncatesAndAppendsMetaLine()
    {
        // Regression: get_violations gab vor Einfuehrung von maxResults die komplette,
        // unbegrenzte Liste zurueck — auf einer Solution mit vielen bestehenden Verstoessen
        // (z. B. Erstlauf gegen ein fremdes Projekt) konnte das den Client-Token-Guard sprengen
        // (dieselbe Bug-Klasse wie get_hotspots vor dessen Fix).
        var fileToProject = new Dictionary<string, string>
        {
            [@"C:\Proj\src\Mini\Foo.cs"] = "SymbolGraphMini",
        };
        var violations = Enumerable.Range(1, 5)
            .Select(i => new RuleViolation
            {
                FilePath = @"C:\Proj\src\Mini\Foo.cs",
                LineNumber = i,
                RuleName = "SomeRule",
                Details = $"Detail {i}",
                Guidance = "Guidance",
            })
            .ToList();

        var text = GetViolationsScanner.FormatReport(
            solutionDir: @"C:\Proj",
            fileToProject: fileToProject,
            violations: violations,
            scopeFilter: null,
            usedDefaultConfig: false,
            maxResults: 2);

        Assert.Contains("5 Verstoesse gesamt, 2 gezeigt", text, StringComparison.Ordinal);
        Assert.Contains("maxResults erhoehen", text, StringComparison.Ordinal);
        // Nur die ersten 2 (nach Datei/Zeile/Regel sortiert) tauchen in den Tabellenzeilen auf.
        Assert.Contains("Detail 1", text, StringComparison.Ordinal);
        Assert.Contains("Detail 2", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Detail 3", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ScopeFilterMatchesProjectName_MaxResultsBelowViolationCount_IsTruncatedSuppressesSufficiencyHint()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(_fixture.Catalog)));

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", 0, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        // maxResults: 0 wird auf 1 normalisiert (analog find_symbol/find_references) — die
        // SymbolGraphMini-Fixture hat mindestens den bekannten ViolationTrigger-Verstoss, ein
        // Limit von 1 muss also trunkieren und darf NICHT den "vollstaendig"-Sufficiency-Hinweis
        // zeigen (der waere hier irrefuehrend).
        Assert.DoesNotContain("Diese Daten sind vollstaendig", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatReport_FilesInScopeButZeroViolations_DistinguishesFromNoFilesInScope()
    {
        var fileToProject = new Dictionary<string, string>
        {
            [@"C:\Proj\src\Mini\Foo.cs"] = "SymbolGraphMini",
        };

        var text = GetViolationsScanner.FormatReport(
            solutionDir: @"C:\Proj",
            fileToProject: fileToProject,
            violations: Array.Empty<RuleViolation>(),
            scopeFilter: "SymbolGraphMini",
            usedDefaultConfig: false);

        Assert.DoesNotContain("Keine Dateien im Scope", text, StringComparison.Ordinal);
        Assert.Contains("Dateien im Scope", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatReport_NoFileMatchesScope_ReturnsExplicitNoFilesMessage()
    {
        var fileToProject = new Dictionary<string, string>
        {
            [@"C:\Proj\src\Mini\Foo.cs"] = "OtherProject",
        };

        var text = GetViolationsScanner.FormatReport(
            solutionDir: @"C:\Proj",
            fileToProject: fileToProject,
            violations: Array.Empty<RuleViolation>(),
            scopeFilter: "SymbolGraphMini",
            usedDefaultConfig: false);

        Assert.Contains("Keine Dateien im Scope", text, StringComparison.Ordinal);
        Assert.Contains("SymbolGraphMini", text, StringComparison.Ordinal);
    }
}
