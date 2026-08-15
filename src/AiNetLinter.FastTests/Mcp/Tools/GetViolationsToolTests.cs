#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.Analysis;
using AiNetLinter.Models;
using AiNetLinter.FastTests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools;

[Trait("Category", "Component")]
public sealed class GetViolationsToolTests
{
    private readonly McpInMemoryTestContext _fixture;

    public GetViolationsToolTests() { _fixture = new McpInMemoryTestContext(); }

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
        var state = _fixture.CreateServer();

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
        var state = _fixture.CreateServer();

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
        var state = _fixture.CreateServer();

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
        var state = _fixture.CreateServer();

        var result = await GetViolationsTool.ExecuteAsync(state, "DoesNotExistAnywhere", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Keine Dateien im Scope", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_LoadedSolutionWithViolation_FormatsViolationsAsMarkdownTable()
    {
        var state = _fixture.CreateServer();

        var result = await GetViolationsTool.ExecuteAsync(state, "SymbolGraphMini", GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("| Datei | Zeile | Regel | Details |", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CompileErrorFixture_DoesNotIncludeCompileErrorsAsViolations()
    {
        using var context = new McpInMemoryTestContext(CompileErrorMiniSolutionSpec.CreatePlural());
        using var state = context.CreateServer();

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
        using var faulty = new FaultingSolutionFixture();
        using var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(null, ReadOnlySolutionSnapshot: faulty.Solution)));

        var result = await GetViolationsTool.ExecuteAsync(state, null, GetViolationsScanner.DefaultMaxResults, CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("ANALYSIS_FAILED", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Einmal erneut versuchen", textContent.Text, StringComparison.Ordinal);
        // Context-Feld (rohe Exception-Message) landet im Text — Nachweis fuer den context:-Fix.
        Assert.Contains("Simulierter Lesefehler", textContent.Text, StringComparison.Ordinal);
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
        var state = _fixture.CreateServer();

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

    [Fact]
    public void FormatReport_ForwardSlashScopeFilterMatchesWindowsPaths_DoesNotReturnNoFiles()
    {
        var fileToProject = new Dictionary<string, string>
        {
            [@"C:\Proj\src\Mini\Foo.cs"] = "OtherProject",
        };

        var text = GetViolationsScanner.FormatReport(
            solutionDir: @"C:\Proj",
            fileToProject: fileToProject,
            violations: Array.Empty<RuleViolation>(),
            scopeFilter: "src/Mini",
            usedDefaultConfig: false);

        Assert.DoesNotContain("Keine Dateien im Scope", text, StringComparison.Ordinal);
        Assert.Contains("Dateien im Scope", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_IncludeSnippetTrue_AppendsCodeSnippetToTextAndStructuredContent()
    {
        var state = _fixture.CreateServer();

        var result = await GetViolationsTool.ExecuteAsync(
            state, new GetViolationsToolExecutionOptions(ScopeFilter: "SymbolGraphMini", ContextLines: 0, IncludeSnippet: true), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("```csharp", textContent.Text, StringComparison.Ordinal);

        var violations = result.StructuredContent!.Value.GetProperty("violations")
            .Deserialize<List<RuleViolation>>(McpJsonOptions.Default);
        Assert.NotNull(violations);
        Assert.NotEmpty(violations!);
        Assert.All(violations!, v => Assert.NotNull(v.Snippet));
    }

    [Fact]
    public async Task ExecuteAsync_IncludeSnippetWithContextLines_IncludesSurroundingLines()
    {
        var state = _fixture.CreateServer();

        var result = await GetViolationsTool.ExecuteAsync(
            state, new GetViolationsToolExecutionOptions(ScopeFilter: "SymbolGraphMini", ContextLines: 2, IncludeSnippet: true), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var violations = result.StructuredContent!.Value.GetProperty("violations")
            .Deserialize<List<RuleViolation>>(McpJsonOptions.Default);
        Assert.NotNull(violations);
        var first = violations!.First(v => !string.IsNullOrEmpty(v.Snippet));
        Assert.Contains("\n", first.Snippet);
    }

    [Fact]
    public async Task ExecuteAsync_IncludeSnippetFalse_SnippetPropertyIsNull()
    {
        var state = _fixture.CreateServer();

        var result = await GetViolationsTool.ExecuteAsync(
            state, new GetViolationsToolExecutionOptions(ScopeFilter: "SymbolGraphMini", ContextLines: 0, IncludeSnippet: false), CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("```csharp", textContent.Text, StringComparison.Ordinal);

        var violations = result.StructuredContent!.Value.GetProperty("violations")
            .Deserialize<List<RuleViolation>>(McpJsonOptions.Default);
        Assert.NotNull(violations);
        Assert.All(violations!, v => Assert.Null(v.Snippet));
    }
}
