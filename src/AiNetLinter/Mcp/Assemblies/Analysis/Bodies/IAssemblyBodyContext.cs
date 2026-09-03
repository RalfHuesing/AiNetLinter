#nullable enable

using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Bodies;

internal interface IAssemblyBodyContext
{
    Solution? Solution { get; }

    AnalysisSymbolIdentity? AssemblySymbolIdentity { get; }
}
