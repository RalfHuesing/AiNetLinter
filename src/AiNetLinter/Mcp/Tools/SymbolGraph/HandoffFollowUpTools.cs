#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.SymbolGraph;

/// <summary>Gemeinsamer Vertrag fuer erlaubte Folge-Tools kanonischer Handoff-IDs.</summary>
internal static class HandoffFollowUpTools
{
    internal static IReadOnlyList<string> For(ISymbol symbol) =>
        symbol is INamedTypeSymbol
            ? ["get_symbol_body", "get_class_structure", "get_type_hierarchy", "find_implementations", "find_references", "get_call_tree"]
            : ["get_symbol_body", "find_references", "get_call_tree", "get_impact", "get_test_context"];
}
