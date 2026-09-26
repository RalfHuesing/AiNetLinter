#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.Verify;

public sealed partial class VerifyToolContractE2ETests
{
    [Fact]
    public async Task GetVerifyAdvisories_LargePopulation_ReportsWholeEntriesWithinUtf8Budget()
    {
        using var fixture = CreateGitFixture();
        var sources = VerifyAdvisoryTestData.ManyUnusedMemberFiles().ToArray();
        foreach (var source in sources)
        {
            var path = Path.Combine(fixture.RootPath, source.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source.Content);
        }
        var typeReferences = sources.Select(source =>
            $"System.Console.WriteLine(typeof({source.RelativePath[4..^3].Replace('/', '.')}));");
        File.WriteAllLines(
            Path.Combine(fixture.RootPath, "src", "BaselineMini", "Program.cs"),
            typeReferences);

        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));
        var advisoryAttempt = await TryGetVerifyAdvisoriesAsync(host);
        var verify = await host.CallToolAsync("verify");

        var verifyText = Assert.IsType<TextContentBlock>(Assert.Single(verify.Content)).Text;
        Assert.True(verifyText.Contains("deadCode: status=complete", StringComparison.Ordinal), verifyText);
        var deadCodeSummary = verifyText.Split('\n').Single(line => line.StartsWith("deadCode:", StringComparison.Ordinal));
        var verifyCandidates = ExtractSummaryCount(deadCodeSummary, "candidates");
        Assert.InRange(verifyCandidates, 714, int.MaxValue);
        Assert.True(verifyText.StartsWith("verdict: pass", StringComparison.Ordinal), verifyText);

        Assert.Null(advisoryAttempt.Error);
        var advisoryText = Assert.IsType<TextContentBlock>(Assert.Single(advisoryAttempt.Result!.Content)).Text;
        var advisoryCandidates = ExtractSummaryCount(advisoryText.Split('\n')[0], "candidates");
        Assert.InRange(advisoryCandidates, verifyCandidates, int.MaxValue);
        AssertVerifyResult(advisoryAttempt.Result!, expectedError: false,
            "status=complete",
            $"candidates={advisoryCandidates}",
            "truncatedBy=");
        Assert.InRange(Encoding.UTF8.GetByteCount(advisoryText), 1, 65_536);
        var shown = ExtractSummaryCount(advisoryText, "shown");
        var truncatedBy = ExtractSummaryCount(advisoryText.Split('\n')[0], "truncatedBy");
        Assert.Equal(advisoryCandidates, shown + truncatedBy);
        Assert.InRange(shown, 1, advisoryCandidates - 1);
        Assert.True(
            advisoryText.Contains("truncat", StringComparison.OrdinalIgnoreCase)
            || advisoryText.Contains("ausgelassen", StringComparison.OrdinalIgnoreCase)
            || advisoryText.Contains("gekürzt", StringComparison.OrdinalIgnoreCase));

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var page = advisoryAttempt.Result!;
        var offset = 0;
        while (true)
        {
            var pageText = Assert.IsType<TextContentBlock>(Assert.Single(page.Content)).Text;
            Assert.True(Encoding.UTF8.GetByteCount(pageText) <= 65_536);
            Assert.Contains("columns: line | symbol | symbolIdentifier | usage | reason | countercheck", pageText, StringComparison.Ordinal);
            var header = pageText.Split('\n')[0];
            Assert.Equal(advisoryCandidates, ExtractSummaryCount(header, "candidates"));
            Assert.Equal(offset, ExtractSummaryCount(header, "offset"));
            var pageShown = ExtractSummaryCount(header, "shown");
            var entries = pageText.Split('\n')
                .Where(line => Regex.IsMatch(line, @"^\d+ \|", RegexOptions.CultureInvariant))
                .ToArray();
            Assert.Equal(pageShown, entries.Length);
            foreach (var entry in entries)
            {
                var fields = entry.Split(" | ");
                Assert.Equal(6, fields.Length);
                Assert.StartsWith("h:", fields[2], StringComparison.Ordinal);
                Assert.True(identifiers.Add(fields[2]), fields[2]);
                Assert.True(fields[3] is "test_only" or "unreferenced");
                Assert.False(string.IsNullOrWhiteSpace(fields[4]));
                Assert.False(string.IsNullOrWhiteSpace(fields[5]));
            }

            offset += pageShown;
            Assert.Equal(advisoryCandidates - offset, ExtractSummaryCount(header, "truncatedBy"));
            var token = pageText.Split('\n').Single(line => line.StartsWith("continuationToken=", StringComparison.Ordinal))["continuationToken=".Length..];
            if (token == "none")
            {
                Assert.Contains("listCompleteness=complete", header, StringComparison.Ordinal);
                break;
            }

            Assert.Contains("listCompleteness=partial", header, StringComparison.Ordinal);
            page = await host.CallToolAsync(
                "get_verify_advisories",
                new Dictionary<string, object?> { ["category"] = "dead_code", ["continuationToken"] = token });
            AssertVerifyResult(page, expectedError: false);
        }

        Assert.Equal(advisoryCandidates, identifiers.Count);
    }

}
