#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Ein einzelner Member eines C#-Typs für die Ausgabe von <c>get_class_structure</c>.
/// </summary>
public sealed record ClassStructureMemberEntry(
    string Kind,
    string Name,
    string Visibility,
    int StartLine,
    int EndLine,
    int LineCount,
    string Signature,
    string FilePath);

/// <summary>
/// Structured-Content-Wurzel für das MCP-Tool <c>get_class_structure</c>.
/// </summary>
public sealed record ClassStructurePayload(
    string TypeName,
    string Kind,
    IReadOnlyList<string> Files,
    int TotalLines,
    int MemberCount,
    IReadOnlyList<ClassStructureMemberEntry> Members);
