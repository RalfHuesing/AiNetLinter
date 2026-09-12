#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp.Tools.Verify;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.Verify;

[Trait("Category", "Integration")]
public sealed class VerifyToolContractE2ETests
{
    [Fact]
    public async Task Verify_ChangedCleanSource_ReturnsPassWithFixedGateSummary()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "VerificationProbe.cs"), """
            namespace BaselineMini;

            public sealed class VerificationProbe
            {
                public int Value => 1;
            }
            """);
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: pass", "requiredScore: 10.0", "requiredViolationCount: 0", "score: 10.0", "violationCount: 0", "operation:", "completeness:", "evidence:", "scope:");
    }

    [Fact]
    public async Task Verify_MultipleChangedCleanSources_UsesTheirCompletePopulation()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "FirstProbe.cs"), """
            namespace BaselineMini;

            public sealed class FirstProbe
            {
                public int Value => 1;
            }
            """);
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "SecondProbe.cs"), """
            namespace BaselineMini;

            public sealed class SecondProbe
            {
                public int Value => 2;
            }
            """);
        FixtureGit.Run(fixture.RootPath, "add src/BaselineMini/FirstProbe.cs");
        File.AppendAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "FirstProbe.cs"), "\n// staged and unstaged coverage\n");
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: pass", "populations: [src/BaselineMini/FirstProbe.cs, src/BaselineMini/SecondProbe.cs]");
    }

    [Fact]
    public async Task Verify_RuleConfigurationChange_ExpandsTheGatePopulationConservatively()
    {
        using var fixture = CreateGitFixture();
        File.AppendAllText(fixture.ConfigPath, "\n");
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "requested: changes", "effective: solution", "populations: [solution]");
    }

    [Fact]
    public async Task Verify_ChangedViolation_ReturnsFailedWithCanonicalEvidenceHandoff()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "UnsealedProbe.cs"), """
            namespace BaselineMini;

            public class UnsealedProbe
            {
            }
            """);
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "violationCount:", "entries:", "handoffId:", "operation:", "completeness:");
    }

    [Fact]
    public async Task Verify_ContentUsesTheFixedServerSideUtf8Budget()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "BudgetProbe.cs"), """
            namespace BaselineMini;

            public class BudgetProbe
            {
            }
            """);
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.True(Encoding.UTF8.GetByteCount(text) <= VerifyTool.ResponseBudgetBytes);
        Assert.Contains("truncationReason: none", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_EmptyWorkingTree_ReturnsIncompleteWithSolutionRecovery()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: incomplete", "decisionReason:", "scope: solution", "operation:", "completeness:");
    }

    [Fact]
    public async Task Verify_InvalidScope_IsRejectedBeforeAnalysisAsContractV2Error()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "diff" });

        AssertVerifyResult(result, expectedError: true, "verdict: error", "operation=error", "completeness=not_applicable", "INVALID_ARGUMENT", "fieldPath: $.scope");

        var validResult = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "solution" });

        AssertVerifyResult(validResult, expectedError: false, "verdict: failed", "scope:", "requested: solution");
    }

    [Fact]
    public async Task Verify_AssemblyTarget_IsRejectedBeforeLeaseOrAnalysis()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync(
            "verify",
            new Dictionary<string, object?> { ["targetPath"] = Path.Combine(fixture.RootPath, "artifacts", "probe.dll") });

        AssertVerifyResult(result, expectedError: true, "verdict: error", "operation=error", "completeness=not_applicable", "ASSEMBLY_TARGET_UNSUPPORTED");
    }

    [Fact]
    public async Task Verify_SolutionScope_UsesFullGateWithoutAdvisoryCandidates()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "solution" });

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "requested: solution", "effective: solution", "score:", "violationCount:");
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("kind: advisory_candidate", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_Changes_ProjectsDeterministicAdvisoryCandidatesWithoutBlockingTheGate()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "AdvisoryProbe.cs"), """
            namespace BaselineMini;

            public sealed class AdvisoryProbe
            {
                private static int Unused() => 42;

                public string Endpoint => "https://example.invalid";

                public string BackupEndpoint => "https://example.invalid";
            }
            """);
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var first = await host.CallToolAsync("verify");
        var second = await host.CallToolAsync("verify");

        AssertVerifyResult(first, expectedError: false,
            "verdict: pass",
            "kind: advisory_candidate",
            "requiresAgentJudgment: true",
            "confidence:",
            "evidenceBoundary:",
            "counterIndicators: [Reflection, DI, Generatoren",
            "category: dead_code",
            "category: magic_value:",
            "advisoryTotalCount: 2",
            "advisoryCompleteness: complete");
        var firstText = Assert.IsType<TextContentBlock>(Assert.Single(first.Content)).Text;
        Assert.True(
            firstText.IndexOf("category: dead_code", StringComparison.Ordinal)
            < firstText.IndexOf("category: magic_value:", StringComparison.Ordinal));
        Assert.Equal(firstText, Assert.IsType<TextContentBlock>(Assert.Single(second.Content)).Text);
    }

    [Fact]
    public async Task Verify_MultipleChangedSources_AggregatesAdvisoryCandidatesAcrossTheFullPopulation()
    {
        using var fixture = CreateGitFixture();
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "FirstAdvisoryProbe.cs"), AdvisoryProbeSource("FirstAdvisoryProbe", "first"));
        File.WriteAllText(Path.Combine(fixture.RootPath, "src", "BaselineMini", "SecondAdvisoryProbe.cs"), AdvisoryProbeSource("SecondAdvisoryProbe", "second"));
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false,
            "verdict: pass",
            "advisoryTotalCount: 4",
            "advisoryCompleteness: complete");
    }

    [Fact]
    public async Task ToolsList_ContainsVerifyAndRetainsOnlyUnaffectedInspectionTools()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var tools = await host.ListToolsAsync();
        var names = new HashSet<string>(tools.Select(tool => tool.Name), StringComparer.Ordinal);

        Assert.Contains("verify", names);
        foreach (var retiredName in new[] { "safeguard", "get_violations", "find_magic_values", "find_dead_code" })
        {
            Assert.DoesNotContain(retiredName, names);
        }

        foreach (var untouchedName in new[] { "pattern_detect", "get_hotspots", "metrics_tree", "metrics_lookup" })
        {
            Assert.Contains(untouchedName, names);
        }
    }

    private static BaselineMiniFixtureWorkspace CreateGitFixture()
    {
        var fixture = new BaselineMiniFixtureWorkspace();
        FixtureGit.Run(fixture.RootPath, "init");
        FixtureGit.Run(fixture.RootPath, "config user.email ainetlinter-verify@example.com");
        FixtureGit.Run(fixture.RootPath, "config user.name AiNetLinterVerifyTest");
        FixtureGit.Run(fixture.RootPath, "add -A");
        FixtureGit.Run(fixture.RootPath, "commit -m initial");
        return fixture;
    }

    private static string AdvisoryProbeSource(string typeName, string route) => $$"""
        namespace BaselineMini;

        public sealed class {{typeName}}
        {
            private static void Unused() { }

            public string Primary => "https://example.invalid/{{route}}";

            public string Secondary => "https://example.invalid/{{route}}";
        }
        """;

    private static void AssertVerifyResult(CallToolResult result, bool expectedError, params string[] expectedContent)
    {
        Assert.Equal(expectedError, result.IsError == true);
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.False(string.IsNullOrWhiteSpace(text));
        foreach (var expected in expectedContent)
        {
            Assert.Contains(expected, text, StringComparison.Ordinal);
        }
    }
}
