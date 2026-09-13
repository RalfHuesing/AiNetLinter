#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.Mcp.Tools.FileStructure;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using SharpToken;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

/// <summary>
/// Misst die vier verbindlichen Toolarten gegen einen kontrollierten, reproduzierbaren
/// Fixture-Snapshot. Die Legacy-Antwort wird aus exakt denselben gerenderten Antworten
/// durch Rückübersetzung jedes Handles rekonstruiert; damit kann keine Fachinformation
/// außerhalb der technischen Handoff-Adresse verloren gehen.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HandoffPayloadMeasurementTests
{
    private static readonly Regex RenderedHandoffPattern = new(
        @"(?<prefix>(?:handoffId|callerId):\s*`)(?<handle>h:[a-zA-Z0-9]+)(?<suffix>`)",
        RegexOptions.CultureInvariant);

    private static readonly Regex AnyRenderedHandoffPattern = new(
        @"(?<prefix>(?:handoffId|callerId):\s*`)(?:h:[a-zA-Z0-9]+|[sa]:[^`\s]+)(?<suffix>`)",
        RegexOptions.CultureInvariant);

    [Fact]
    public async Task ReferenceResponses_ReduceBytesAndO200kTokensWithoutLosingEvidence()
    {
        using var source = new McpInMemoryTestContext();
        using var assemblyTemp = TestTempDirectory.Create("handoff-payload-measurement-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(assemblyTemp, "HandoffMeasure", """
            namespace Probe;
            public sealed class PublicApi
            {
                public string Describe(int value) => value.ToString();
                public int Increment(int value) => value + 1;
            }
            """);

        var responses = new[]
        {
            (Name: "find_symbol", Text: TextOf(await FindSymbolTool.ExecuteAsync(
                source.CreateServer(), ["Greet"], kind: null, maxResults: 50, CancellationToken.None))),
            (Name: "get_file_skeleton", Text: TextOf(await GetFileSkeletonTool.ExecuteAsync(
                source.CreateServer(), ["src/SymbolGraphMini/Greeter.cs", "src/SymbolGraphMini/Caller.cs"], CancellationToken.None))),
            (Name: "find_references", Text: TextOf(await FindReferencesTool.ExecuteAsync(
                source.CreateServer(), "Greeter.Greet", maxResults: 50, depth: 1, CancellationToken.None))),
            (Name: "inspect_assembly", Text: TextOf(await InspectAssemblyToolDispatch.ExecuteAsync(
                null, new InspectAssemblyArguments(assemblyPath, null, null, null, true, 100), CancellationToken.None))),
        };

        var encoding = GptEncoding.GetEncoding("o200k_base");
        var measurements = responses.Select(response => Measure(response.Name, response.Text, encoding)).ToArray();

        Assert.All(measurements, measurement =>
        {
            Assert.True(measurement.OpaqueHandoffCharacters > 0, $"{measurement.Name} enthält keinen Handoff.");
            Assert.True(measurement.OpaqueUtf8Bytes <= measurement.LegacyUtf8Bytes, measurement.ToString());
            Assert.True(measurement.OpaqueTokens <= measurement.LegacyTokens, measurement.ToString());
            Assert.Equal(measurement.NormalizedLegacyText, measurement.NormalizedOpaqueText);
        });

        var totalLegacyBytes = measurements.Sum(measurement => measurement.LegacyUtf8Bytes);
        var totalOpaqueBytes = measurements.Sum(measurement => measurement.OpaqueUtf8Bytes);
        var totalLegacyHandoffCharacters = measurements.Sum(measurement => measurement.LegacyHandoffCharacters);
        var totalOpaqueHandoffCharacters = measurements.Sum(measurement => measurement.OpaqueHandoffCharacters);
        var totalLegacyTokens = measurements.Sum(measurement => measurement.LegacyTokens);
        var totalOpaqueTokens = measurements.Sum(measurement => measurement.OpaqueTokens);

        Assert.True(totalOpaqueBytes * 100 <= totalLegacyBytes * 75,
            $"UTF-8-Ersparnis kleiner als 25 %: legacy={totalLegacyBytes}, opaque={totalOpaqueBytes}.");
        Assert.True(totalOpaqueHandoffCharacters * 100 <= totalLegacyHandoffCharacters * 30,
            $"Handoff-Zeichenersparnis kleiner als 70 %: legacy={totalLegacyHandoffCharacters}, opaque={totalOpaqueHandoffCharacters}.");
        Assert.True(totalOpaqueTokens < totalLegacyTokens,
            $"o200k_base-Tokenzahl nicht gesunken: legacy={totalLegacyTokens}, opaque={totalOpaqueTokens}.");
    }

    private static PayloadMeasurement Measure(string name, string opaqueText, GptEncoding encoding)
    {
        var legacyText = RenderedHandoffPattern.Replace(opaqueText, match =>
        {
            var restored = HandoffHandleRegistry.Default.RestoreInternalHandoffForInput(match.Groups["handle"].Value);
            Assert.True(restored.IsSuccess, $"{name}: Handle konnte nicht restauriert werden.");
            return match.Groups["prefix"].Value + restored.Value + match.Groups["suffix"].Value;
        });

        var opaqueHandoffCharacters = HandoffCharacters(opaqueText);
        var legacyHandoffCharacters = HandoffCharacters(legacyText);
        return new PayloadMeasurement(
            name,
            Encoding.UTF8.GetByteCount(legacyText),
            Encoding.UTF8.GetByteCount(opaqueText),
            encoding.CountTokens(legacyText),
            encoding.CountTokens(opaqueText),
            legacyHandoffCharacters,
            opaqueHandoffCharacters,
            NormalizeHandoffs(legacyText),
            NormalizeHandoffs(opaqueText));
    }

    private static int HandoffCharacters(string text) =>
        AnyRenderedHandoffPattern.Matches(text).Sum(match => match.Value.Length - match.Groups["prefix"].Value.Length - match.Groups["suffix"].Value.Length);

    private static string NormalizeHandoffs(string text) =>
        AnyRenderedHandoffPattern.Replace(text, "${prefix}{handoff}${suffix}");

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private sealed record PayloadMeasurement(
        string Name,
        int LegacyUtf8Bytes,
        int OpaqueUtf8Bytes,
        int LegacyTokens,
        int OpaqueTokens,
        int LegacyHandoffCharacters,
        int OpaqueHandoffCharacters,
        string NormalizedLegacyText,
        string NormalizedOpaqueText)
    {
        public override string ToString() =>
            $"{Name}: legacy={LegacyUtf8Bytes} B/{LegacyTokens} Tok/{LegacyHandoffCharacters} Handoff-Zeichen, " +
            $"opaque={OpaqueUtf8Bytes} B/{OpaqueTokens} Tok/{OpaqueHandoffCharacters} Handoff-Zeichen";
    }
}
