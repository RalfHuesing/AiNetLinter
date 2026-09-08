#nullable enable

using System;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Fixtures;

internal sealed class McpInMemoryTestContext : IDisposable
{
    private readonly RoslynTestSolution owner;

    public McpInMemoryTestContext()
        : this(SymbolGraphMiniSolutionSpec.Create())
    {
    }

    public McpInMemoryTestContext(RoslynTestSolution owner)
    {
        this.owner = owner;
    }

    public McpCodeGraphServer CreateServer(
        int? maxLineCount = null,
        Config? config = null,
        string? resolvedConfigPath = null,
        bool includeConfig = true) => new(
        McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            null,
            MaxLineCount: maxLineCount ?? 700,
            Config: includeConfig
                ? config ?? new Config
                {
                    Global = new GlobalConfig(),
                    Metrics = new MetricsConfig(),
                }
                : null,
            ResolvedConfigPath: resolvedConfigPath,
            ReadOnlySolutionSnapshot: owner.Solution)));

    public Solution Solution => owner.Solution;

    public static RoslynTestSolution CreateScenario(params ProjectSpec[] projects) =>
        RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\McpScenario.slnx", projects);

    public void Dispose() => owner.Dispose();
}
