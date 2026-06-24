#nullable enable

using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Web;
using Xunit;

// @covers RazorConfig (StaticTestSentinel: Kognitive Komplexitaet 6 > Schwellwert 5; Konfiguration ist ueber diese Tests abgedeckt.)
namespace AiNetLinter.Tests.Web;

/// <summary>
/// Unit-Tests fuer RazorAnalyzer. Implementiert die Test-Szenarien A-N aus
/// Research/Extend-Web-Features/03_Razor_Linting.md Abschnitt 8.
/// Weitere Tests (Edge-Cases, Helper-Methoden, Szenarien-Kombinationen) sind in
/// <see cref="RazorAnalyzerExtendedTests"/> ausgelagert, um die Datei unter MaxLineCount (500) zu halten.
/// </summary>
public sealed class RazorAnalyzerTests
{
    // Szenario A — Saubere Komponente unter 300 Zeilen, flaches Markup,
    // Methoden-Referenzen bei Events → keine Violations.
    [Fact]
    public void Analyze_NoViolations_ForCleanComponent()
    {
        const string razor = """
            @page "/counter"
            @using MyApp.Models

            <PageTitle>Counter</PageTitle>

            <h1>Counter</h1>

            <p role="status">Current count: @currentCount</p>

            <button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

            @code {
                private int currentCount = 0;

                private void IncrementCount()
                {
                    currentCount++;
                }
            }
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Counter.razor", config);

        Assert.Empty(violations);
    }

    // Szenario B — Razor-Datei mit 350 Zeilen → RAZOR_MaxRazorLineCount.
    [Fact]
    public void Analyze_ReportsMaxRazorLineCount_WhenFileExceedsLimit()
    {
        var lines = Enumerable.Range(1, 350).Select(i => $"<div>Line {i}</div>");
        var razor = string.Join("\n", lines);
        var config = new RazorConfig { MaxRazorLineCount = 300 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Huge.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxRazorLineCount", violations[0].RuleName);
        Assert.Contains("350", violations[0].Details);
        Assert.Contains("300", violations[0].Details);
    }

    // Szenario C — 7-fach verschachteltes HTML → RAZOR_MaxMarkupNestingDepth.
    [Fact]
    public void Analyze_ReportsMaxMarkupNestingDepth_WhenMarkupTooDeep()
    {
        const string razor = """
            <div>
                <div>
                    <div>
                        <div>
                            <div>
                                <div>
                                    <span>x</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            """;
        var config = new RazorConfig { MaxMarkupNestingDepth = 6 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Deep.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxMarkupNestingDepth", violations[0].RuleName);
        Assert.Contains("7", violations[0].Details);
        Assert.Contains("6", violations[0].Details);
    }

    // Szenario D — @onclick="() => { Count++; Save(); }" → RAZOR_BanInlineEventLambdas.
    [Fact]
    public void Analyze_ReportsBanInlineEventLambdas_WhenMultiStatementLambda()
    {
        const string razor = """
            <button @onclick="() => { Count++; Save(); }">Click</button>
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Counter.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_BanInlineEventLambdas", violations[0].RuleName);
        Assert.Contains("@onclick", violations[0].Details);
    }

    // Szenario E — @onclick="() => Count++" (triviales Einzeiler-Lambda) → keine Violation.
    [Fact]
    public void Analyze_NoViolation_ForTrivialInlineLambda()
    {
        const string razor = """
            <button @onclick="() => Count++">Click</button>
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Counter.razor", config);

        Assert.Empty(violations);
    }

    // Szenario F — 10 @if-Bloecke in einer Datei → RAZOR_MaxControlFlowBlocks.
    [Fact]
    public void Analyze_ReportsMaxControlFlowBlocks_WhenTooMany()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 10; i++)
        {
            sb.AppendLine($"@if (State{i}) {{ <p>Case {i}</p> }}");
        }
        var razor = sb.ToString();
        var config = new RazorConfig { MaxControlFlowBlocks = 8 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\ManyStates.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxControlFlowBlocks", violations[0].RuleName);
        Assert.Contains("10", violations[0].Details);
        Assert.Contains("8", violations[0].Details);
    }

    // Szenario G — Dreifach-verschachtelte @foreach-Schleifen → RAZOR_MaxForeachNestingDepth.
    [Fact]
    public void Analyze_ReportsMaxForeachNestingDepth_WhenTooDeep()
    {
        const string razor = """
            @foreach (var a in A) {
                @foreach (var b in a.Items) {
                    @foreach (var c in b.Items) {
                        <span>@c</span>
                    }
                }
            }
            """;
        var config = new RazorConfig { MaxForeachNestingDepth = 2 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Nested.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxForeachNestingDepth", violations[0].RuleName);
        Assert.Contains("3", violations[0].Details);
        Assert.Contains("2", violations[0].Details);
    }

    // Szenario H — <MyComp A="a" B="b" C="c" D="d" E="e" F="f" /> → RAZOR_MaxComponentParameterCount.
    [Fact]
    public void Analyze_ReportsMaxComponentParameterCount_WhenTooMany()
    {
        const string razor = """
            <MyComp A="a" B="b" C="c" D="d" E="e" F="f" />
            """;
        var config = new RazorConfig { MaxComponentParameterCount = 5 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\TooMany.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxComponentParameterCount", violations[0].RuleName);
        Assert.Contains("MyComp", violations[0].Details);
        Assert.Contains("6", violations[0].Details);
    }

    // Szenario I — class="base @(flag ? "active" : "")" → RAZOR_BanInlineTernaryInAttributes.
    [Fact]
    public void Analyze_ReportsBanInlineTernaryInAttributes_WhenTernaryPresent()
    {
        const string razor = """
            <div class="base @(flag ? "active" : "")">x</div>
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Classy.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_BanInlineTernaryInAttributes", violations[0].RuleName);
    }

    // Szenario J — class="@CssClass" (einfacher Ausdruck ohne Ternary) → keine Violation.
    [Fact]
    public void Analyze_NoViolation_ForSimpleExpression()
    {
        const string razor = """
            <div class="@CssClass">x</div>
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Classy.razor", config);

        Assert.Empty(violations);
    }

    // Szenario K — Direktiven am Dateianfang (@page, @inject, @typeparam) → kein Crash, korrekt ignoriert.
    [Fact]
    public void Analyze_NoCrash_ForDirectivesAtTop()
    {
        const string razor = """
            @page "/generic"
            @typeparam TItem
            @inject NavigationManager Nav
            @using System.Collections.Generic

            <h1>Generic List</h1>
            <ul>
                @foreach (var item in Items) {
                    <li>@item</li>
                }
            </ul>

            @code {
                [Parameter] public IEnumerable<TItem>? Items { get; set; }
            }
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Generic.razor", config);

        Assert.DoesNotContain(violations, v => v.RuleName == "RAZOR_MaxControlFlowBlocks");
    }

    // Szenario L — Razor ohne @code-Block → kein Crash, korrekte Ergebnisse.
    [Fact]
    public void Analyze_NoCrash_WithoutCodeBlock()
    {
        const string razor = """
            @page "/simple"

            <h1>Hello</h1>
            <p>No code-behind here.</p>
            """;
        var config = new RazorConfig();

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Simple.razor", config);

        Assert.Empty(violations);
    }

    // Szenario M — @* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@ → Violation unterdrueckt.
    // Die Suppression wird im WebFileSeparationChecker gehandhabt, nicht im Analyzer selbst.
    [Fact]
    public void Analyze_StillReportsViolation_SuppressionIsHandledByChecker()
    {
        const string razor = """
            @* ainetlinter-disable RAZOR_MaxControlFlowBlocks *@
            @if (A) { <p>x</p> }
            @if (B) { <p>y</p> }
            @if (C) { <p>z</p> }
            @if (D) { <p>w</p> }
            @if (E) { <p>v</p> }
            @if (F) { <p>u</p> }
            @if (G) { <p>t</p> }
            @if (H) { <p>s</p> }
            @if (I) { <p>r</p> }
            @if (J) { <p>q</p> }
            """;
        var config = new RazorConfig { MaxControlFlowBlocks = 8 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Legacy.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxControlFlowBlocks", violations[0].RuleName);
    }

    // Szenario N — <button ...> → KEIN RAZOR_MaxComponentParameterCount (HTML-Native-Tag).
    [Fact]
    public void Analyze_NoMaxComponentParameterCount_ForNativeHtmlTag()
    {
        const string razor = """
            <button class="btn" type="submit" aria-label="Save" disabled="@IsLoading">Save</button>
            """;
        var config = new RazorConfig { MaxComponentParameterCount = 5 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Save.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — @code-Block mit > MaxRazorCodeBlockLines → RAZOR_MaxRazorCodeBlockLines.
    [Fact]
    public void Analyze_ReportsMaxRazorCodeBlockLines_WhenTooLarge()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("@code {");
        for (int i = 0; i < 30; i++)
        {
            sb.AppendLine($"    private int x{i};");
        }
        sb.AppendLine("}");
        var razor = sb.ToString();
        var config = new RazorConfig { MaxRazorCodeBlockLines = 20 };

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Counter.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_MaxRazorCodeBlockLines", violations[0].RuleName);
        Assert.Contains("Counter.razor.cs", violations[0].Guidance);
    }
}