#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.TypeResolution;

internal sealed record TypeOriginInfoDto(
    string AssemblyName,
    string AssemblyPath,
    string FullName,
    string Kind,
    bool IsSource,
    string ContainingNamespace);

internal sealed record ResolveTypeOriginResultDto(
    string TypeName,
    bool Found,
    TypeOriginInfoDto? Origin,
    IReadOnlyList<string> SearchedAssemblies);
