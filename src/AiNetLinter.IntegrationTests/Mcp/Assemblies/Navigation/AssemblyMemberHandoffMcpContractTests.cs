#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies.Navigation;

[Trait("Category", "Integration")]
public sealed class AssemblyMemberHandoffMcpContractTests
{
    [Fact]
    public async Task InspectAssembly_ExplicitConstructorHandoff_IsReusableByBodyAndContext()
    {
        using var fixture = new BaselineMiniFixtureWorkspace();
        using var temp = TestTempDirectory.Create("assembly-member-handoff-mcp-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "AssemblyMemberHandoffProbe",
            """
            namespace Probe;
            public sealed class Api
            {
                public Api(int value) { Value = value; }
                public int Value { get; }
            }
            """);
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var inspection = await host.CallToolAsync(
            "inspect_assembly",
            new Dictionary<string, object?>
            {
                ["targetPath"] = assemblyPath,
                ["typeName"] = "Probe.Api",
                ["exactTypeName"] = true,
                ["maxMembers"] = 10,
            });
        Assert.NotEqual(true, inspection.IsError);
        var memberHandoffs = ExtractMemberHandoffs(inspection);
        Assert.Equal(2, memberHandoffs.Count);

        foreach (var handoff in memberHandoffs)
        {
            var body = await host.CallToolAsync(
                "get_symbol_body",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = assemblyPath,
                    ["symbolIdentifiers"] = new[] { handoff },
                });
            Assert.NotEqual(true, body.IsError);
            Assert.DoesNotContain("SYMBOL_NOT_FOUND", TextOf(body), StringComparison.Ordinal);

            var context = await host.CallToolAsync(
                "get_assembly_context",
                new Dictionary<string, object?>
                {
                    ["targetPath"] = assemblyPath,
                    ["symbolIdentifier"] = handoff,
                });
            Assert.NotEqual(true, context.IsError);
            Assert.DoesNotContain("SYMBOL_NOT_FOUND", TextOf(context), StringComparison.Ordinal);
        }
    }

    private static IReadOnlyList<string> ExtractMemberHandoffs(CallToolResult result)
    {
        var matches = Regex.Matches(
            TextOf(result),
            @"^  - [^\r\n]*handoffId: `(?<id>h:[^`]+)`$",
            RegexOptions.CultureInvariant | RegexOptions.Multiline);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"inspect_assembly muss Member-Handoffs ausgeben: {TextOf(result)}");
        }

        var handoffs = new List<string>(matches.Count);
        foreach (Match match in matches) handoffs.Add(match.Groups["id"].Value);
        return handoffs;
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
}
