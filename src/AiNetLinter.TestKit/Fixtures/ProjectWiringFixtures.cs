#nullable enable

using System;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;

namespace AiNetLinter.TestKit;

internal static class ProjectWiringFixtures
{
    private static readonly Lazy<RoslynTestSolution> Scenario =
        new(SymbolGraphMiniSolutionSpec.Create);

    public static McpCodeGraphServer CreateLoadedServer(Config? config = null) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            null,
            MaxLineCount: 700,
            Config: config,
            ReadOnlySolutionSnapshot: Scenario.Value.Solution)));

    public static ProjectRegistry CreateLoadedRegistry(
        TimeProvider? clock = null,
        Func<McpCodeGraphServer>? createServer = null,
        int maxProjects = 4,
        TimeSpan? idleTtl = null) =>
        ProjectRegistryFixture.Create(
            _ => ProjectInstanceCreation.Resident((createServer ?? (() => CreateLoadedServer()))()),
            clock,
            maxProjects,
            idleTtl);
}
