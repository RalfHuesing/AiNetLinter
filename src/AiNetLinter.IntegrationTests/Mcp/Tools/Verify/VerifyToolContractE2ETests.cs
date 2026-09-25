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
using ModelContextProtocol;
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

        AssertVerifyResult(result, expectedError: false, "verdict: pass", "completeness: complete", "score: 10.0", "violationCount: 0", "scope: changes", "deadCode: status=");
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

        AssertVerifyResult(result, expectedError: false, "verdict: pass", "scope: changes", "deadCode: status=");
    }

    [Fact]
    public async Task Verify_RuleConfigurationChange_ExpandsTheGatePopulationConservatively()
    {
        using var fixture = CreateGitFixture();
        File.AppendAllText(fixture.ConfigPath, "\n");
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "scope: changes -> solution", "deadCode: status=");
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

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "completeness: complete", "findings: count=", "ref=");
        var reference = ExtractReference(result, "- rule=");
        var body = await host.CallToolAsync(
            "get_symbol_body",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = new[] { reference } });

        AssertVerifyResult(body, expectedError: false, "UnsealedProbe");
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
        Assert.Contains("truncation=none", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_EmptyWorkingTree_ReturnsIncompleteWithSolutionRecovery()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify");

        AssertVerifyResult(result, expectedError: false, "verdict: incomplete", "completeness: incomplete", "reason:", "scope: changes", "recovery:");
    }

    [Fact]
    public async Task Verify_InvalidScope_IsRejectedBeforeAnalysisAsContractV2Error()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "diff" });

        AssertVerifyResult(result, expectedError: true, "verdict: error", "INVALID_ARGUMENT", "field: $.scope");

        var validResult = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "solution" });

        AssertVerifyResult(validResult, expectedError: false, "verdict: failed", "scope: solution", "deadCode: status=");
    }

    [Theory]
    [InlineData(null, "INVALID_ARGUMENT")]
    [InlineData("missing-source.slnx", "SOLUTION_NOT_FOUND")]
    [InlineData("wrong-source.cs", "INVALID_ARGUMENT")]
    public async Task Verify_InvalidSourceTarget_UsesContractV2ErrorAndAllowsAValidFollowUp(
        string? relativeTargetPath,
        string expectedCode)
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = relativeTargetPath is null
            ? await host.CallToolWithoutDefaultTargetAsync("verify")
            : await host.CallToolAsync(
                "verify",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = Path.Combine(fixture.RootPath, relativeTargetPath),
                });

        AssertVerifyResult(
            result,
            expectedError: true,
            "verdict: error",
            expectedCode,
            "field: $.targetPath",
            "recovery:");

        var validResult = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "solution" });

        AssertVerifyResult(validResult, expectedError: false, "verdict: failed", "scope: solution", "deadCode: status=");
    }

    [Fact]
    public async Task Verify_AssemblyTarget_IsRejectedBeforeLeaseOrAnalysis()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync(
            "verify",
            new Dictionary<string, object?> { ["targetPath"] = Path.Combine(fixture.RootPath, "artifacts", "probe.dll") });

        AssertVerifyResult(result, expectedError: true, "verdict: error", "ASSEMBLY_TARGET_UNSUPPORTED");
    }

    [Fact]
    public async Task Verify_SolutionScope_UsesFullGateWithoutAdvisoryCandidates()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync("verify", new Dictionary<string, object?> { ["scope"] = "solution" });

        AssertVerifyResult(result, expectedError: false, "verdict: failed", "score: ", "violationCount: ", "scope: solution", "deadCode: status=");
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.DoesNotContain("verdict: error", text, StringComparison.Ordinal);
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
            "advisories: count=",
            "deadCode: status=complete; candidates=",
            "deadCodeHint:",
            "category=dead_code",
            "category=magic_value:",
            "symbolIdentifier=h:");
        var firstText = Assert.IsType<TextContentBlock>(Assert.Single(first.Content)).Text;
        Assert.True(
            firstText.IndexOf("category=dead_code", StringComparison.Ordinal)
            < firstText.IndexOf("category=magic_value:", StringComparison.Ordinal));
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
            "advisories: count=",
            "deadCode: status=complete; candidates=");
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains("ref=src/BaselineMini/FirstAdvisoryProbe.cs:", text, StringComparison.Ordinal);
        Assert.Contains("ref=src/BaselineMini/SecondAdvisoryProbe.cs:", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetVerifyAdvisories_WithoutPriorVerify_ReturnsAllSmallNavigableCandidates()
    {
        const int candidateCount = 30;
        using var fixture = CreateGitFixture();
        File.WriteAllText(
            Path.Combine(fixture.RootPath, "src", "BaselineMini", "ManyUnusedMembers.cs"),
            VerifyAdvisoryTestData.ManyUnusedMembersSource(candidateCount));
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var advisoryAttempt = await TryGetVerifyAdvisoriesAsync(host);
        var verify = await host.CallToolAsync("verify");
        AssertVerifyResult(verify, expectedError: false, "verdict: pass", "deadCode: status=complete");
        var verifyText = Assert.IsType<TextContentBlock>(Assert.Single(verify.Content)).Text;
        var deadCodeSummary = verifyText.Split('\n').Single(line => line.StartsWith("deadCode:", StringComparison.Ordinal));
        var candidates = ExtractSummaryCount(deadCodeSummary, "candidates");
        Assert.Equal(candidateCount + 1, candidates);
        var verifyShown = ExtractSummaryCount(deadCodeSummary, "shown");
        var verifyTruncatedBy = ExtractSummaryCount(deadCodeSummary, "truncatedBy");
        Assert.Equal(ExtractSymbolIdentifiers(verifyText).Count, verifyShown);
        Assert.Equal(candidates - verifyShown, verifyTruncatedBy);
        Assert.InRange(verifyShown, 1, candidates - 1);
        Assert.Contains("next=review_now", deadCodeSummary, StringComparison.Ordinal);

        Assert.Null(advisoryAttempt.Error);
        AssertVerifyResult(advisoryAttempt.Result!, expectedError: false,
            "status=complete",
            $"candidates={candidates}",
            $"shown={candidates}",
            "truncatedBy=0");
        var advisoryText = Assert.IsType<TextContentBlock>(Assert.Single(advisoryAttempt.Result!.Content)).Text;
        Assert.True(Encoding.UTF8.GetByteCount(advisoryText) <= 65_536);
        var identifiers = ExtractSymbolIdentifiers(advisoryText);
        Assert.Equal(candidates, identifiers.Count);
        Assert.Equal(identifiers.Count, identifiers.Distinct(StringComparer.Ordinal).Count());

        var candidateBodies = await host.CallToolAsync(
            "get_symbol_body",
            new Dictionary<string, object?> { ["symbolIdentifiers"] = identifiers.ToArray() });
        AssertVerifyResult(candidateBodies, expectedError: false);
        var candidateBodyHeaders = Assert.IsType<TextContentBlock>(Assert.Single(candidateBodies.Content)).Text
            .Split('\n')
            .Count(line => line.StartsWith("### ", StringComparison.Ordinal));
        Assert.Equal(identifiers.Count, candidateBodyHeaders);
    }

    [Fact]
    public async Task GetVerifyAdvisories_LargePopulation_ReportsWholeEntriesWithinUtf8Budget()
    {
        using var fixture = CreateGitFixture();
        foreach (var source in VerifyAdvisoryTestData.ManyUnusedMemberFiles())
        {
            var path = Path.Combine(fixture.RootPath, source.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source.Content);
        }

        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));
        var advisoryAttempt = await TryGetVerifyAdvisoriesAsync(host);
        var verify = await host.CallToolAsync("verify");

        var verifyText = Assert.IsType<TextContentBlock>(Assert.Single(verify.Content)).Text;
        Assert.Contains("deadCode: status=complete", verifyText, StringComparison.Ordinal);
        var deadCodeSummary = verifyText.Split('\n').Single(line => line.StartsWith("deadCode:", StringComparison.Ordinal));
        var candidates = ExtractSummaryCount(deadCodeSummary, "candidates");
        Assert.InRange(candidates, 714, int.MaxValue);
        Assert.True(verifyText.StartsWith("verdict: pass", StringComparison.Ordinal), verifyText);

        Assert.Null(advisoryAttempt.Error);
        AssertVerifyResult(advisoryAttempt.Result!, expectedError: false,
            "status=complete",
            $"candidates={candidates}",
            "truncatedBy=");
        var advisoryText = Assert.IsType<TextContentBlock>(Assert.Single(advisoryAttempt.Result!.Content)).Text;
        Assert.InRange(Encoding.UTF8.GetByteCount(advisoryText), 1, 65_536);
        var shown = ExtractSummaryCount(advisoryText, "shown");
        var truncatedBy = ExtractSummaryCount(advisoryText, "truncatedBy");
        Assert.Equal(candidates, shown + truncatedBy);
        Assert.InRange(shown, 1, candidates - 1);
        Assert.True(
            advisoryText.Contains("truncat", StringComparison.OrdinalIgnoreCase)
            || advisoryText.Contains("ausgelassen", StringComparison.OrdinalIgnoreCase)
            || advisoryText.Contains("gekürzt", StringComparison.OrdinalIgnoreCase));

        var entryLines = advisoryText.Split('\n').Where(line => line.Contains("symbolIdentifier=", StringComparison.Ordinal)).ToArray();
        Assert.Equal(shown, entryLines.Length);
        Assert.All(entryLines, line =>
        {
            Assert.Contains("line=", line, StringComparison.Ordinal);
            Assert.Contains("usage=", line, StringComparison.Ordinal);
            Assert.Contains("confidence=", line, StringComparison.Ordinal);
        });
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

    private static async Task<(CallToolResult? Result, McpProtocolException? Error)> TryGetVerifyAdvisoriesAsync(McpProcessHost host)
    {
        try
        {
            return (await host.CallToolAsync(
                "get_verify_advisories",
                new Dictionary<string, object?> { ["category"] = "dead_code" }), null);
        }
        catch (McpProtocolException exception)
        {
            return (null, exception);
        }
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

    private static List<string> ExtractSymbolIdentifiers(string text)
    {
        var identifiers = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            const string marker = "symbolIdentifier=";
            var start = line.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0)
            {
                continue;
            }

            start += marker.Length;
            var end = line.IndexOf(';', start);
            var identifier = line[start..(end < 0 ? line.Length : end)].Trim();
            if (identifier.Length > 0)
            {
                identifiers.Add(identifier);
            }
        }

        return identifiers;
    }

    private static int ExtractSummaryCount(string summary, string key)
    {
        var field = summary.Split(';').Single(value => value.TrimStart().StartsWith($"{key}=", StringComparison.Ordinal));
        var value = field[(field.IndexOf('=') + 1)..].Trim();
        return int.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string ExtractReference(CallToolResult result, string entryPrefix)
    {
        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        var entry = Assert.Single(text.Split('\n').Where(line => line.StartsWith(entryPrefix, StringComparison.Ordinal)));
        const string prefix = "; ref=";
        const string suffix = "; reason=";
        var start = entry.IndexOf(prefix, StringComparison.Ordinal) + prefix.Length;
        var end = entry.IndexOf(suffix, StringComparison.Ordinal);
        return entry[start..end];
    }

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
