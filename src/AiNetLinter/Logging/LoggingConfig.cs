#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Logging;

/// <summary>
/// Konfiguration des prozessinternen System-Loggings (Serilog-Datei-Sink).
/// Alle Prozessrollen schreiben in den gemeinsamen System-Log-Sink.
/// </summary>
internal sealed record LoggingConfig(
    string MinimumLevel,
    string Directory,
    int RetainedFileCount,
    bool McpCallLogging = true)
{
    internal const string DefaultMinimumLevel = "Debug";
    internal const string DefaultDirectoryName = "logs";
    internal const int DefaultRetainedFileCount = 14;

    internal static readonly IReadOnlyList<string> AllowedLevels = new[]
    {
        "Verbose",
        "Debug",
        "Information",
        "Warning",
        "Error",
        "Fatal",
    };

    internal static LoggingConfig CreateDefault() => new(
        DefaultMinimumLevel,
        DefaultDirectoryName,
        DefaultRetainedFileCount);

    /// <summary>Aufloesung relativ zum EXE-Verzeichnis (AppContext.BaseDirectory).</summary>
    internal string ResolveDirectory() =>
        Path.IsPathRooted(this.Directory)
            ? this.Directory
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, this.Directory));
}
