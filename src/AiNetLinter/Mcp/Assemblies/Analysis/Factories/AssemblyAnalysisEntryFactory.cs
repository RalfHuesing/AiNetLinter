#nullable enable

using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Factories;

internal static class AssemblyAnalysisEntryFactory
{
    internal static AssemblyAnalysisEntry Create(AssemblyAnalysisEntryCreateParameters parameters)
    {
        var state = CreateReadOnlyStateProvider(parameters.Solution, parameters.Context);
        var entry = new AssemblyAnalysisEntry(
            parameters.CanonicalPath,
            state,
            state,
            parameters.Context,
            new(
                parameters.Lifetime,
                parameters.ResourceLease,
                parameters.OnReferenceLeaseReleased,
                parameters.ReferenceLeaseFactory));
        entry.SetClock(parameters.Clock);
        return entry;
    }

    private static McpCodeGraphServer CreateReadOnlyStateProvider(
        Solution solution,
        AssemblyContext context) =>
        new(McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            Catalog: null,
            Config: new Config
            {
                Global = new GlobalConfig(),
                Metrics = new MetricsConfig(),
            },
            ReadOnlySolutionSnapshot: solution,
            AssemblySymbolIdentity: new AnalysisSymbolIdentity(
                context.Origin.ContentHash,
                context.Generation))));
}
