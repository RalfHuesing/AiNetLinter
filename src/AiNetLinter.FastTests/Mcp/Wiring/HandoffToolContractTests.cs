#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Tools;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Wiring;

/// <summary>
/// Prüft die von <c>tools/list</c> abgeleitete, echte MCP-Schemaoberfläche aller Symbol-Handoff-Eingänge.
/// Das verhindert, dass Framework-Parameter oder neue Symbolfelder unbemerkt am öffentlichen Vertrag landen.
/// </summary>
[Trait("Category", "Component")]
public sealed class HandoffToolContractTests
{
    [Fact]
    public async Task ToolCollection_PublishesExactlyTheKnownPublicSymbolHandoffFields()
    {
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectRegistryFixture.CreateInspectionRegistry();
        var tools = McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions)),
                assemblyRegistry: composition.Sessions)
            .ToDictionary(item => item.ProtocolTool.Name, item => item.ProtocolTool, StringComparer.Ordinal);

        var actual = tools
            .SelectMany(pair => GetProperties(pair.Value.InputSchema)
                .Where(HandoffFieldNames.Contains)
                .Select(field => $"{pair.Key}.{field}")
                .Where(field => !PublicNonHandoffFields.Contains(field)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedPublicHandoffFields.OrderBy(value => value, StringComparer.Ordinal), actual);
        Assert.DoesNotContain("context", GetProperties(tools["find_references"].InputSchema));
    }

    private static readonly HashSet<string> HandoffFieldNames = new(StringComparer.Ordinal)
    {
        "helperSymbol",
        "symbolIdentifier",
        "symbolIdentifiers",
        "typeName",
    };

    private static readonly string[] ExpectedPublicHandoffFields =
    [
        "dependency_graph.symbolIdentifier",
        "find_duplicates.helperSymbol",
        "find_implementations.symbolIdentifier",
        "find_references.symbolIdentifier",
        "get_assembly_context.symbolIdentifier",
        "get_call_tree.symbolIdentifier",
        "get_class_structure.symbolIdentifier",
        "get_feature_context.symbolIdentifier",
        "get_impact.symbolIdentifier",
        "get_symbol_body.symbolIdentifiers",
        "get_test_context.symbolIdentifier",
        "get_type_hierarchy.symbolIdentifier",
        "metrics_lookup.symbolIdentifiers",
        "resolve_type_origin.typeName",
        "resolve_type_origin.symbolIdentifier",
    ];

    // inspect_assembly.typeName ist ausschließlich ein textueller Assembly-Filter; es nimmt
    // weder h:-Handles entgegen noch durchläuft einen Symbolresolver.
    private static readonly HashSet<string> PublicNonHandoffFields = new(StringComparer.Ordinal)
    {
        "inspect_assembly.typeName",
    };

    private static IEnumerable<string> GetProperties(JsonElement inputSchema)
    {
        using var document = JsonDocument.Parse(inputSchema.ToString());
        return document.RootElement.GetProperty("properties").EnumerateObject().Select(property => property.Name).ToArray();
    }
}
