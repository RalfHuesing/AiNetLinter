#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiNetLinter.Mcp;

/// <summary>
/// Zentrale <see cref="JsonSerializerOptions"/> für intern benötigte MCP-Tool-Payloads.
/// Einmal zentral statt in jedem Tool eine eigene Instanz, damit weitere
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
