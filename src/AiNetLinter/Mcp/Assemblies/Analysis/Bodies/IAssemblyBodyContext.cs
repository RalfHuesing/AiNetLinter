#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Bodies;

internal interface IAssemblyBodyContext
{
    Solution? Solution { get; }

    AnalysisSymbolIdentity? AssemblySymbolIdentity { get; }

    bool IsDecompiled { get; }

    Task<AssemblyBodyResolution> ResolveBodyAsync(
        ISymbol symbol,
        int maxBodyLines,
        CancellationToken cancellationToken);
}
