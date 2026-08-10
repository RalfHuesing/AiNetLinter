#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiNetLinter.Mcp;

/// <summary>
/// Zentrale <see cref="JsonSerializerOptions"/> fuer alle MCP-Tool-<c>StructuredContent</c>-Payloads
/// (S1.3 Structured-Output-Mode, <c>tasks/features/05-roadmap.md</c> Z. 92). Vorher als private
/// Instanz in <see cref="Tools.SafeguardTool"/> dupliziert — hier einmal zentral, damit weitere
/// Tools (<see cref="Tools.GetViolationsTool"/>, <see cref="Tools.GetHotspotsTool"/> etc.) dieselben
/// Optionen ohne 7+ identische Instanzen wiederverwenden. CamelCase (JSON-Konvention statt C#-
/// PascalCase), kompakt (<c>WriteIndented=false</c> spart Tokens im Agent-Kontext), null-Felder
/// weggelassen.
/// </summary>
internal static class McpJsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
