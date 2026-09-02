#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.References;

/// <summary>
/// Traversal-only view of a reference session. Lease ownership stays with
/// <see cref="AssemblyAnalysisLease"/> while the expander only receives the
/// data and operation it needs for graph traversal.
/// </summary>
internal sealed record AssemblyReferenceExpansionNode(
    string AssemblyPath,
    AssemblyIdentityDto? Identity,
    AssemblyOrigin? Origin,
    string Completeness,
    string SessionStatus,
    IReadOnlyList<string> Diagnostics,
    IReadOnlyList<AssemblyReferenceDto> References,
    Func<AssemblyReferenceDto, CancellationToken, Task<AssemblyReferenceExpansionNode?>> OpenReferenceAsync);
