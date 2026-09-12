#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.MetricsLookup;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.MetricsLookup;

[Trait("Category", "Component")]
public sealed class MetricsLookupToolTests
{
    private readonly McpInMemoryTestContext _fixture = new();

    [Fact]
    public async Task ExecuteAsync_NoSolutionLoaded_ReturnsErrorWithSolutionNotLoadedCode()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(null)));

        var result = await MetricsLookupTool.ExecuteAsync(state, ["irrelevant"], CancellationToken.None);

        Assert.True(result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SOLUTION_NOT_LOADED", textContent.Text);
    }

    public static IEnumerable<object?[]> EmptyCases =>
    [
        [null],
        [System.Array.Empty<string>()],
        [new[] { "", "   " }]
    ];

    [Theory]
    [MemberData(nameof(EmptyCases))]
    public async Task ExecuteAsync_EmptySymbolIdentifiers_ReturnsRecoverableInvalidArgument(string[]? symbolIdentifiers)
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, symbolIdentifiers, CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text);
        Assert.Contains("Pflichtparameter 'symbolIdentifiers' fehlt oder ist leer.", textContent.Text);
        Assert.Contains("symbolIdentifiers: [\"M:Klasse.Methode\"]", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentSymbol_ReturnsRecoverableSymbolNotFound()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["NonExistentClass.NonExistentMethod"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text);
    }

    [Fact]
    public async Task ExecuteAsync_SingleSymbol_ReportsResolvedSymbolInContent()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["Greeter.Greet"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Greet", text, StringComparison.Ordinal);
        Assert.Contains("Method:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MethodSymbol_ReturnsMethodMetricsAndThresholds()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["Greeter.Greet"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text);
        Assert.Contains("Schwellwert-Abgleich", textContent.Text);
        Assert.Contains("[OK]", textContent.Text);

        Assert.Contains("Parameter-Anzahl (effektiv)", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Zyklomatische Komplexität", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeSymbol_ReturnsTypeMetricsAndFootprint()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["Greeter"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greeter", textContent.Text);
        Assert.Contains("AI-Context-Footprint", textContent.Text);

        Assert.Contains("Typ-Struktur", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Members:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_PropertySymbol_ReturnsPropertyMetrics()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["Greeter.Prefix"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Prefix", textContent.Text);

        Assert.Contains("Property-Details", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("Getter & Setter", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ComplexMethodWithViolations_ReportsViolationsCorrectly()
    {
        using var customSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ComplexTest.slnx",
            new ProjectSpec("ComplexTest", [
                ("Complex.cs", """
                    namespace ComplexTest;

                    public class ComplexClass
                    {
                        public int ComplexMethod(int a, int b, int c, int d, int e)
                        {
                            if (a > 0)
                            {
                                if (b > 0)
                                {
                                    while (c > 0)
                                    {
                                        c--;
                                    }
                                }
                            }
                            return a + b + c + d + e;
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/ComplexTest"));

        using var customFixture = new McpInMemoryTestContext(customSolution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 2,
                MaxCognitiveComplexity = 2
            }
        };

        var state = customFixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, ["ComplexClass.ComplexMethod"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Parameter-Anzahl (effektiv)", text, StringComparison.Ordinal);
        Assert.Contains("Zyklomatische Komplexität", text, StringComparison.Ordinal);
        Assert.Contains("[VIOLATION]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_IgnoredParameterTypes_ExcludesFromEffectiveCount()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\IgnoreParamTest.slnx",
            new ProjectSpec("IgnoreParamTest", [
                ("ParamTest.cs", """
                    using System.Threading;

                    namespace IgnoreParamTest;

                    public class ParamTestClass
                    {
                        public void Execute(string name, int count, CancellationToken ct)
                        {
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/IgnoreParamTest"));

        using var fixture = new McpInMemoryTestContext(solution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MethodParameterCountIgnoreTypeNames = ["CancellationToken"],
                MaxMethodParameterCount = 4
            }
        };

        var state = fixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, ["ParamTestClass.Execute"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Parameter-Anzahl (effektiv)", text, StringComparison.Ordinal);
        Assert.Contains("ct (CancellationToken)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithMultipleAutoProperties_DoesNotInflatePublicOrTotalMemberCounts()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\PropertyMemberCountTest.slnx",
            new ProjectSpec("PropertyMemberCountTest", [
                ("UserDto.cs", """
                    namespace PropertyMemberCountTest;

                    public class UserDto
                    {
                        public string FirstName { get; set; } = "";
                        public string LastName { get; set; } = "";
                        public int Age { get; init; }
                        public string Email { get; set; } = "";

                        public void DoWork() {}
                    }
                    """)
            ], VirtualProjectDirectory: "src/PropertyMemberCountTest"));

        using var fixture = new McpInMemoryTestContext(solution);
        var state = fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(state, ["UserDto"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Members:** 5 gesamt (5 public, 1 Methoden, 4 Properties)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_TypeWithExemptSuffix_MarksMaxPublicMembersPerTypeAsOk()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\ExemptSuffixTest.slnx",
            new ProjectSpec("ExemptSuffixTest", [
                ("AppSettingsConfig.cs", """
                    namespace ExemptSuffixTest;

                    public class AppSettingsConfig
                    {
                        public string P1 { get; set; } = "";
                        public string P2 { get; set; } = "";
                        public string P3 { get; set; } = "";
                        public string P4 { get; set; } = "";
                        public string P5 { get; set; } = "";
                        public string P6 { get; set; } = "";
                    }
                    """)
            ], VirtualProjectDirectory: "src/ExemptSuffixTest"));

        using var fixture = new McpInMemoryTestContext(solution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MaxPublicMembersPerType = 3,
                MaxPublicMembersPerTypeExemptSuffixes = ["Config"]
            }
        };

        var state = fixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, ["AppSettingsConfig"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("Public Member Anzahl", text, StringComparison.Ordinal);
        Assert.Contains("6", text, StringComparison.Ordinal);
        Assert.Contains("[OK]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MethodWithCompoundSuppression_AppliesRelaxedLimit()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\CompoundMethodTest.slnx",
            new ProjectSpec("CompoundMethodTest", [
                ("SimpleLongMethod.cs", """
                    namespace CompoundMethodTest;

                    public class Sample
                    {
                        public void LongSimpleMethod()
                        {
                            var a = 1;
                            var b = 2;
                            var c = 3;
                            var d = 4;
                            var e = 5;
                            var f = 6;
                            var g = 7;
                            var h = 8;
                            var i = 9;
                            var j = 10;
                        }
                    }
                    """)
            ], VirtualProjectDirectory: "src/CompoundMethodTest"));

        using var fixture = new McpInMemoryTestContext(solution);
        var config = new Config
        {
            Global = new(),
            Metrics = new MetricsConfig
            {
                MaxMethodLineCount = 5,
                CompoundSuppressions =
                [
                    new CompoundSuppression
                    {
                        TargetRule = LinterRuleIds.MaxMethodLineCount,
                        WhenAllOf =
                        [
                            new MetricCondition { Metric = MetricNames.CyclomaticComplexity, AtMost = 2 },
                            new MetricCondition { Metric = MetricNames.CognitiveComplexity, AtMost = 2 }
                        ],
                        RelaxedLimit = 20
                    }
                ]
            }
        };

        var state = fixture.CreateServer(config: config);

        var result = await MetricsLookupTool.ExecuteAsync(state, ["Sample.LongSimpleMethod"], CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("<= 20", text, StringComparison.Ordinal);
        Assert.Contains("[OK]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleSymbols_ReturnsAllMetricsInSingleTurn()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(
            state,
            symbolIdentifiers: ["Greeter.Greet", "Greeter.Prefix"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text);
        Assert.Contains("Prefix", textContent.Text);
        Assert.Contains("---", textContent.Text);

    }

    [Fact]
    public async Task ExecuteAsync_MultipleSymbols_WithOneNotFound_ContinuesAndIncludesWarning()
    {
        var state = _fixture.CreateServer();

        var result = await MetricsLookupTool.ExecuteAsync(
            state,
            symbolIdentifiers: ["Greeter.Greet", "DoesNotExistXyz"],
            ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Greet", textContent.Text);
        Assert.Contains("DoesNotExistXyz", textContent.Text);
        Assert.Contains("nicht aufgeloest", textContent.Text);
    }
}
