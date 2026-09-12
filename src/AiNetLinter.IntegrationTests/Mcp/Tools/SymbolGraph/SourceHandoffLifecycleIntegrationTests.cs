#nullable enable

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Commands;
using AiNetLinter.IntegrationTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.SymbolGraph;

[Trait("Category", "Integration")]
public sealed class SourceHandoffLifecycleIntegrationTests
{
    [Fact]
    public async Task SourceHandoff_SurvivesEvictionReloadAndNewRegistryInstance()
    {
        using var fixture = new SymbolGraphMiniFixtureWorkspace();
        var clock = new ManualTimeProvider();
        var handoffId = string.Empty;

        await using (var registry = CreateRegistry(clock))
        {
            var first = registry.Lease(fixture.SolutionPath);
            Assert.True(first.Succeeded);
            using var firstLease = first.Lease!;
            var firstServer = firstLease.Server;
            await TestWaiter.WaitForConditionAsync(
                () => firstServer.LoadState == ServerLoadState.Loaded,
                TimeSpan.FromSeconds(30));

            var discovery = await FindSymbolTool.ExecuteAsync(
                firstServer,
                ["Greeter"],
                "class",
                maxResults: 50,
                CancellationToken.None);
            Assert.False(discovery.IsError == true, Text(discovery));
            handoffId = ExtractHandoffId(Text(discovery));
            Assert.StartsWith("s:", handoffId, StringComparison.Ordinal);

            firstLease.Dispose();
            clock.Advance(TimeSpan.FromMinutes(2));
            await registry.RunEvictionTickAsync();

            var reloaded = registry.Lease(fixture.SolutionPath);
            Assert.True(reloaded.Succeeded);
            using var reloadedLease = reloaded.Lease!;
            Assert.NotSame(firstServer, reloadedLease.Server);
            await TestWaiter.WaitForConditionAsync(
                () => reloadedLease.Server.LoadState == ServerLoadState.Loaded,
                TimeSpan.FromSeconds(30));

            var afterEviction = await FindReferencesTool.ExecuteAsync(
                reloadedLease.Server,
                handoffId,
                maxResults: 50,
                depth: 1,
                CancellationToken.None);
            Assert.False(afterEviction.IsError == true, Text(afterEviction));
        }

        await using var restartedRegistry = CreateRegistry(clock);
        var restarted = restartedRegistry.Lease(fixture.SolutionPath);
        Assert.True(restarted.Succeeded);
        using var restartedLease = restarted.Lease!;
        await TestWaiter.WaitForConditionAsync(
            () => restartedLease.Server.LoadState == ServerLoadState.Loaded,
            TimeSpan.FromSeconds(30));

        var afterRestart = await FindReferencesTool.ExecuteAsync(
            restartedLease.Server,
            handoffId,
            maxResults: 50,
            depth: 1,
            CancellationToken.None);
        Assert.False(afterRestart.IsError == true, Text(afterRestart));
    }

    private static ProjectRegistry CreateRegistry(TimeProvider clock) =>
        new(new ProjectRegistryOptions(
            definition => McpServerCommand.CreateResidentInstance(definition, LinterConsole.Instance),
            clock,
            MaxProjects: 1,
            IdleTtl: TimeSpan.FromMinutes(1)));

    private static string Text(ModelContextProtocol.Protocol.CallToolResult result) =>
        result.Content.OfType<ModelContextProtocol.Protocol.TextContentBlock>().SingleOrDefault()?.Text
        ?? result.ToString()
        ?? string.Empty;

    private static string ExtractHandoffId(string text)
    {
        var match = Regex.Match(text, @"(?:handoffId|id): `(?<id>s:[^`]+)`", RegexOptions.CultureInvariant);
        return match.Success
            ? match.Groups["id"].Value
            : throw new InvalidOperationException("Der Content muss eine kopierbare Source-Handoff-ID enthalten.");
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }
}
