#nullable enable

using System;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Fixtures;

/// <summary>
/// Wiring-Test-Bausteine: sofort geladene Server-Instanzen ueber einen geteilten
/// In-Memory-Roslyn-Snapshot (kein Hintergrund-Load, kein MSBuild) und daraus
/// gebaute Registries.
/// </summary>
internal static class ProjectWiringFixtures
{
    private static readonly Lazy<RoslynTestSolution> Scenario =
        new(SymbolGraphMiniSolutionSpec.Create);

    /// <summary>Server mit fertigem ReadOnly-Snapshot: LoadState ist sofort Loaded.</summary>
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
