#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>Ein physischer Dateitreffer des <c>get_file_tree</c>-Scans.</summary>
public sealed record FileTreeFileEntry(
    string Path,
    string? Extension,
    long? SizeBytes,
    int? LineCount,
    int Depth);

/// <summary>Aggregierte Informationen eines Verzeichnisses mit Dateitreffern.</summary>
public sealed record FileTreeDirectoryEntry(
    string Path,
    int Depth,
    int MatchedFileCount,
    long MatchedBytes,
    int ChildDirectoryCount);

/// <summary>Verteilung der gematchten Dateien nach letzter Extension.</summary>
public sealed record FileTreeExtensionEntry(
    string? Extension,
    int Count,
    long Bytes);

/// <summary>Gesamtzahlen des physischen File-Tree-Walks.</summary>
public sealed record FileTreeSummary(
    int ScannedFileCount,
    int MatchedFileCount,
    int ScannedDirectoryCount,
    int MatchedDirectoryCount,
    long MatchedBytes,
    IReadOnlyList<FileTreeExtensionEntry> ByExtension);

/// <summary>Vollstaendigkeits- und Trunkierungsinformationen des Scans.</summary>
public sealed record FileTreeCompleteness(
    bool ScanCompleted,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy,
    int ShownFileCount,
    int InaccessibleSubtreeCount,
    int SkippedExcludedDirectoryCount,
    int SkippedReparsePointCount,
    IReadOnlyList<string> Warnings);

/// <summary>Kanonische strukturierte Antwort fuer <c>get_file_tree</c>.</summary>
public sealed record FileTreePayload(
    string Root,
    string EffectiveRoot,
    string View,
    FileTreeSummary Summary,
    IReadOnlyList<FileTreeDirectoryEntry> Directories,
    IReadOnlyList<FileTreeFileEntry> Files,
    FileTreeCompleteness Completeness);

/// <summary>Internes Scanresult, das Renderer und Structured Content gemeinsam verwenden.</summary>
internal sealed record FileTreeScanResult(FileTreePayload Payload, int TreeDepth);
