#nullable enable

using AiNetLinter.Mcp;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>
/// Fachlicher Suchauftrag fuer den Symbol-Scanner. Die Suchparameter bleiben
/// zusammen, waehrend die Cancellation separat als Laufzeitsteuerung uebergeben wird.
/// </summary>
internal sealed record FindSymbolScanRequest(
    Solution Solution,
    string NamePattern,
    string? Kind,
    int MaxResults,
    AnalysisSymbolIdentity? AssemblyIdentity = null);
