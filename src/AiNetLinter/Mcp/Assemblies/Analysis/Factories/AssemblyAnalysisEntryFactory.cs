#nullable enable

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
            ReadOnlySolutionSnapshot: solution,
            AssemblySymbolIdentity: AnalysisSymbolIdentity.ForAssembly(
                context.Origin.CanonicalPath,
                context.Origin.ContentHash,
                context.Generation))));
}
