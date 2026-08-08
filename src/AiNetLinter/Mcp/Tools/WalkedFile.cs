#nullable enable

namespace AiNetLinter.Mcp.Tools;

/// <summary>Relativer und absoluter Pfad einer per Walk gefundenen Datei.</summary>
internal readonly record struct WalkedFile(string RelativePath, string AbsolutePath);
