#nullable enable

using AiNetLinter.Configuration;
using AiNetLinter.Web;
using Xunit;

namespace AiNetLinter.Tests.Web;

/// <summary>
/// Erweiterte Tests fuer RazorAnalyzer: Edge-Cases, deaktivierte Regeln, Szenarien-Kombinationen
/// und direkte Tests der internen Helper-Methoden. Aufgeteilt in eigene Datei, um die
/// Haupt-Test-Datei unter MaxLineCount (500) zu halten.
/// </summary>
public sealed class RazorAnalyzerExtendedTests
{
    // Zusatztest — Leere Datei liefert keine Violations.
    [Fact]
    public void Analyze_NoViolations_ForEmptyContent()
    {
        var config = RazorTestConfig.Default;
        var violations = RazorAnalyzer.Analyze("", "C:\\app\\Pages\\Empty.razor", config);
        Assert.Empty(violations);
    }

    // Zusatztest — Null-Content liefert keine Violations.
    [Fact]
    public void Analyze_NoViolations_ForNullContent()
    {
        var config = RazorTestConfig.Default;
        var violations = RazorAnalyzer.Analyze(null!, "C:\\app\\Pages\\Null.razor", config);
        Assert.Empty(violations);
    }

    // Zusatztest — Self-closing Tags zaehlen NICHT zur Verschachtelungstiefe.
    [Fact]
    public void Analyze_NoNestingViolation_ForSelfClosingTags()
    {
        const string razor = """
            <div>
                <br />
                <hr />
                <input type="text" />
                <MyComponent Value="x" />
            </div>
            """;
        var config = RazorTestConfig.Default.With(maxNesting: 2);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Void.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — HTML-Kommentare werden bei der Tiefenberechnung ignoriert.
    [Fact]
    public void Analyze_NoNestingViolation_WhenTagsInComments()
    {
        const string razor = """
            <div>
                <!-- <span><b><i>commented</i></b></span> -->
                <p>real content</p>
            </div>
            """;
        var config = RazorTestConfig.Default.With(maxNesting: 3);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Comments.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — Razor-Kommentare werden ebenfalls ignoriert.
    [Fact]
    public void Analyze_NoNestingViolation_WhenTagsInRazorComments()
    {
        const string razor = """
            <div>
                @* <span><b><i><u>razor comment</u></i></b></span> *@
                <p>real content</p>
            </div>
            """;
        var config = RazorTestConfig.Default.With(maxNesting: 3);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\RazorComments.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — Methode-Referenz bei Event-Handler ist erlaubt.
    [Fact]
    public void Analyze_NoViolation_ForEventMethodReference()
    {
        const string razor = """
            <button @onclick="HandleClick">Click</button>
            <input @bind="Text" />
            """;
        var config = RazorTestConfig.Default;

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Form.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — MaxControlFlowBlocks deaktiviert (Limit 0) ueberspringt Pruefung.
    [Fact]
    public void Analyze_NoMaxControlFlowBlocks_WhenLimitIsZero()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"@if (S{i}) {{ <p>{i}</p> }}");
        }
        var razor = sb.ToString();
        var config = RazorTestConfig.Default.With(maxControlFlow: 0);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Many.razor", config);

        Assert.DoesNotContain(violations, v => v.RuleName == "RAZOR_MaxControlFlowBlocks");
    }

    // Zusatztest — BanInlineEventLambdas deaktiviert unterdrueckt Lambda-Check.
    [Fact]
    public void Analyze_NoBanInlineEventLambdas_WhenDisabled()
    {
        const string razor = """
            <button @onclick="() => { DoSomething(); DoMore(); }">x</button>
            """;
        var config = RazorTestConfig.Default.With(banInlineEventLambdas: false);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Lib.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — BanInlineTernaryInAttributes deaktiviert unterdrueckt Ternary-Check.
    [Fact]
    public void Analyze_NoBanInlineTernaryInAttributes_WhenDisabled()
    {
        const string razor = """
            <div class="@(cond ? "a" : "b")">x</div>
            """;
        var config = RazorTestConfig.Default.With(banInlineTernary: false);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Classy.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — MaxForeachNestingDepth deaktiviert (Limit 0) ueberspringt Pruefung.
    [Fact]
    public void Analyze_NoMaxForeachNestingDepth_WhenLimitIsZero()
    {
        const string razor = """
            @foreach (var a in A) {
                @foreach (var b in a.Items) {
                    @foreach (var c in c.Items) {
                        @foreach (var d in d.Items) {
                            <span>x</span>
                        }
                    }
                }
            }
            """;
        var config = RazorTestConfig.Default.With(maxForeachDepth: 0);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Deep.razor", config);

        Assert.DoesNotContain(violations, v => v.RuleName == "RAZOR_MaxForeachNestingDepth");
    }

    // Zusatztest — @foreach-Body mit geschachteltem @if zaehlt korrekt.
    [Fact]
    public void Analyze_HandlesForeachWithNestedIf()
    {
        const string razor = """
            @foreach (var item in items) {
                @if (item.IsActive) {
                    <span>@item.Name</span>
                }
            }
            """;
        var config = RazorTestConfig.Default.With(maxForeachDepth: 2);

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Items.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — Mehrere separate Razor-Regeln koennen parallel zueinander feuern.
    [Fact]
    public void Analyze_ReportsMultipleViolations_WhenMultipleRulesTriggered()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 350; i++)
        {
            sb.AppendLine($"<div>Line {i}</div>");
        }
        for (int i = 0; i < 9; i++)
        {
            sb.AppendLine($"@if (S{i}) {{ <p>{i}</p> }}");
        }
        var razor = sb.ToString();
        var config = RazorTestConfig.Default;

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Multi.razor", config);

        Assert.Contains(violations, v => v.RuleName == "RAZOR_MaxRazorLineCount");
        Assert.Contains(violations, v => v.RuleName == "RAZOR_MaxControlFlowBlocks");
    }

    // Zusatztest — Inline-Ternary ohne Leerzeichen vor @() wird ebenfalls erkannt.
    [Fact]
    public void Analyze_DetectsTernary_WithoutLeadingWhitespace()
    {
        const string razor = """
            <div class="@(x?"a":"b")">x</div>
            """;
        var config = RazorTestConfig.Default;

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Tight.razor", config);

        Assert.Single(violations);
        Assert.Equal("RAZOR_BanInlineTernaryInAttributes", violations[0].RuleName);
    }

    // Zusatztest — Methoden-Referenz auf Property zaehlt NICHT als Lambda.
    [Fact]
    public void Analyze_NoViolation_ForPropertyReference()
    {
        const string razor = """
            <button @onclick="@OnClick">x</button>
            """;
        var config = RazorTestConfig.Default;

        var violations = RazorAnalyzer.Analyze(razor, "C:\\app\\Pages\\Ref.razor", config);

        Assert.Empty(violations);
    }

    // Zusatztest — ComputeMaxTagNestingDepth: void und self-closing Tags ignorieren.
    [Fact]
    public void ComputeMaxTagNestingDepth_SkipsVoidAndSelfClosing()
    {
        const string content = """
            <div>
                <br />
                <input />
                <MyComp />
                <span>x</span>
            </div>
            """;

        Assert.Equal(2, RazorAnalyzer.ComputeMaxTagNestingDepth(content));
    }

    // Zusatztest — ComputeMaxTagNestingDepth zaehlt korrekt.
    [Fact]
    public void ComputeMaxTagNestingDepth_CountsNestedTags()
    {
        const string content = """
            <a><b><c><d><e>x</e></d></c></b></a>
            """;

        Assert.Equal(5, RazorAnalyzer.ComputeMaxTagNestingDepth(content));
    }

    // Zusatztest — ComputeMaxForeachNestingDepth erkennt Verschachtelung.
    [Fact]
    public void ComputeMaxForeachNestingDepth_DetectsDeepNesting()
    {
        const string content = """
            @foreach (var a in A) {
                @foreach (var b in B) {
                    <span>x</span>
                }
            }
            """;

        Assert.Equal(2, RazorAnalyzer.ComputeMaxForeachNestingDepth(content));
    }

    // Zusatztest — CountAttributes zaehlt @-Praefix korrekt.
    [Fact]
    public void CountAttributes_CountsAtPrefixedAttributes()
    {
        const string attrs = """ @bind-Value="x" @bind-Value:event="oninput" Disabled="true" """;

        Assert.Equal(3, RazorAnalyzer.CountAttributes(attrs));
    }
}

/// <summary>
/// Test-Helper: Builder-Pattern fuer RazorConfig-Varianten.
/// Vermeidet einen 8-Parameter-Konstruktor (verletzt MaxMethodParameterCount).
/// </summary>
internal static class RazorTestConfig
{
    public static RazorConfig Default { get; } = new RazorConfig();

    public static RazorConfig With(
        RazorConfig source,
        int? maxLines = null,
        int? maxCodeBlockLines = null,
        int? maxNesting = null,
        bool? banInlineEventLambdas = null,
        int? maxControlFlow = null,
        int? maxForeachDepth = null,
        int? maxComponentParams = null,
        bool? banInlineTernary = null) =>
        source with
        {
            MaxRazorLineCount = maxLines ?? source.MaxRazorLineCount,
            MaxRazorCodeBlockLines = maxCodeBlockLines ?? source.MaxRazorCodeBlockLines,
            MaxMarkupNestingDepth = maxNesting ?? source.MaxMarkupNestingDepth,
            BanInlineEventLambdas = banInlineEventLambdas ?? source.BanInlineEventLambdas,
            MaxControlFlowBlocks = maxControlFlow ?? source.MaxControlFlowBlocks,
            MaxForeachNestingDepth = maxForeachDepth ?? source.MaxForeachNestingDepth,
            MaxComponentParameterCount = maxComponentParams ?? source.MaxComponentParameterCount,
            BanInlineTernaryInAttributes = banInlineTernary ?? source.BanInlineTernaryInAttributes,
        };
}

/// <summary>
/// Convenience-Erweiterung: ermoeglicht <c>RazorTestConfig.Default.With(...)</c> statt
/// <c>RazorTestConfig.With(RazorTestConfig.Default, ...)</c>.
/// </summary>
internal static class RazorTestConfigExtensions
{
    public static RazorConfig With(
        this RazorConfig source,
        int? maxLines = null,
        int? maxCodeBlockLines = null,
        int? maxNesting = null,
        bool? banInlineEventLambdas = null,
        int? maxControlFlow = null,
        int? maxForeachDepth = null,
        int? maxComponentParams = null,
        bool? banInlineTernary = null) =>
        RazorTestConfig.With(
            source,
            maxLines,
            maxCodeBlockLines,
            maxNesting,
            banInlineEventLambdas,
            maxControlFlow,
            maxForeachDepth,
            maxComponentParams,
            banInlineTernary);
}