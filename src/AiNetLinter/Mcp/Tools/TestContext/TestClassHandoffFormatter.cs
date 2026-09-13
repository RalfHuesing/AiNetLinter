#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiNetLinter.Mcp.Handoffs;

namespace AiNetLinter.Mcp.Tools.TestContext;

/// <summary>
/// Rendert klassenbasierte Test-Evidenz als direkte, konsumierbare Handoffs.
/// </summary>
internal static class TestClassHandoffFormatter
{
    internal static void Append(StringBuilder sb, IReadOnlyList<TestClassHandoff>? testClasses)
    {
        if (testClasses is not { Count: > 0 }) return;
        var classes = testClasses.Select(testClass =>
            $"`{testClass.Name}` (handoffId: `{HandoffHandleRegistry.Default.GetOpaqueHandleForOutputOrThrow(testClass.Id)}`)");
        sb.AppendLine($"  - testKlassen: {string.Join(", ", classes)}");
    }
}
