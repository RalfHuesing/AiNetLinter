#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.TypeResolution;

internal sealed record TypeOriginSourceLocationDto(string Path, int Line, int Column);

internal sealed record TypeOriginInfoDto(
    string FullName,
    string Kind,
    string TargetPath,
    string? ProjectName,
    IReadOnlyList<TypeOriginSourceLocationDto> SourceLocations,
    string AssemblyOrigin,
    string? OutputAssembly,
    string ContainingNamespace);

internal sealed record ResolveTypeOriginResultDto(
    string TypeName,
    bool Found,
    TypeOriginInfoDto? Origin,
    IReadOnlyList<string> SearchedAssemblies);
