#nullable enable

using System.Collections.Generic;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

/// <summary>
/// Wiederverwendbare Hilfsmethoden zum Bauen von <see cref="CallToolResult"/>-Instanzen fuer
/// MCP-Tools — buendelt sowohl die Protokoll-Ebene (<see cref="CallToolResult.IsError"/>) als auch
/// das bestehende Text-Fehlerformat (<see cref="LinterErrorFormatter"/>), damit jedes Tool dasselbe
/// Boilerplate nicht einzeln nachbaut.
/// </summary>
internal static class McpToolResults
{
    /// <summary>
    /// Baut ein Fehlerergebnis: <see cref="CallToolResult.IsError"/> ist <see langword="true"/>, der
    /// Text folgt dem bestehenden <c>[ERROR]</c>-Format aus <see cref="LinterErrorFormatter"/>.
    /// </summary>
    internal static CallToolResult Error(string code, string message, string? context = null, string? hint = null)
    {
        var text = LinterErrorFormatter.Format(code, message, context, hint);
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }

    /// <summary>
    /// Kurzform fuer den in jedem Tool wiederkehrenden Fall, dass beim Serverstart keine Solution
    /// geladen werden konnte (<see cref="McpCodeGraphServer.IsLoaded"/> ist <see langword="false"/>).
    /// </summary>
    internal static CallToolResult SolutionNotLoaded()
    {
        return Error(
            LinterErrorCodes.SolutionNotLoaded,
            "Solution ist nicht geladen — der MCP-Server konnte beim Start keine gueltige Solution laden.",
            hint: "Server-Log auf [WARN]-Zeilen zum Ladefehler pruefen.");
    }

    /// <summary>
    /// Baut ein Erfolgsergebnis mit genau einem Text-Content-Block.
    /// </summary>
    internal static CallToolResult Text(string text)
    {
        return new CallToolResult
        {
            Content = new List<ContentBlock> { new TextContentBlock { Text = text } },
        };
    }
}
