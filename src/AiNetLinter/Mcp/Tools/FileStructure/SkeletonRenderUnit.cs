#nullable enable

using System.Collections.Generic;
using AiNetLinter.Maps.Skeleton;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>Rendered markdown and structured types for one file-level skeleton unit.</summary>
internal sealed record SkeletonRenderUnit(
    string Markdown,
    string Path,
    IReadOnlyList<SkeletonTypeInfo> Types);
