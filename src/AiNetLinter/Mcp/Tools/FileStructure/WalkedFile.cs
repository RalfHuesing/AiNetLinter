#nullable enable

using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>Relativer und absoluter Pfad einer per Walk gefundenen Datei.</summary>
internal readonly record struct WalkedFile(string RelativePath, string AbsolutePath, Document Document);
