#nullable enable

using System;

namespace AiNetLinter.Mcp;

/// <summary>
/// mtime + Inhalts-Hash einer vom MCP-Server beobachteten Datei. Wird beim Start
/// einmal initial befuellt, beim Verzeichnis-Sweep fuer neu einghaengte Dateien
/// uebernommen und beim modifizierten-Datei-Check inkrementell aktualisiert.
/// </summary>
internal readonly record struct McpFileState(DateTime MtimeUtc, string Hash);
