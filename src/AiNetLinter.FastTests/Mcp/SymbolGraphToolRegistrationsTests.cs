#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Regressionstests fuer die in <see cref="SymbolGraphToolRegistrations"/> gepflegten
/// Tool-Beschreibungen. Sichert ab, dass der 200-Knoten-Hard-Cap aus
/// <c>CallGraphTraversal.MaxRecursionNodes</c> fuer die beiden Tools, die ihn tatsaechlich
/// nutzen (<c>find_references</c> und <c>get_impact</c>), im Tool-Schema dokumentiert ist —
/// sonst sieht ein Agent das Limit erst in der Trunkierungs-Meta-Zeile, nachdem der Cap
/// bereits erreicht wurde.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SymbolGraphToolRegistrationsTests
{
    [Fact]
    public void ToolDescriptions_FindReferencesAndGetImpact_MentionNodeHardCap()
    {
        var options = McpServerOptionsFactory.Create(ProjectRegistryFixture.CreateInspectionRegistry());

        var descriptions = options.ToolCollection!
            .ToDictionary(t => t.ProtocolTool.Name, t => t.ProtocolTool.Description!);

        Assert.Contains("200", descriptions["find_references"], StringComparison.Ordinal);
        Assert.Contains("200", descriptions["get_impact"], StringComparison.Ordinal);
    }
}
