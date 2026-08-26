#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed record InspectAssemblyArguments(
    string? AssemblyPath,
    string? Namespace,
    string? TypeName,
    string? MemberName,
    bool PublicOnly,
    int MaxResults);

internal sealed record FindAssemblyExtensionsArguments(
    string? AssemblyPath,
    string? ReceiverType,
    string? ExtensionName,
    string? Namespace,
    int MaxResults);

internal sealed record AssemblyInspectionOptions(
    string? NamespaceFilter,
    string? TypeFilter,
    string? MemberFilter,
    bool PublicOnly,
    int MaxResults);

internal sealed record AssemblyExtensionSearchOptions(
    string? ExtensionName,
    string? NamespaceFilter,
    int MaxResults);

internal sealed record AssemblyTypeSelection(
    IReadOnlyList<AssemblyTypeDto> Items,
    IReadOnlyList<string> Namespaces,
    int Total,
    bool Truncated);

internal sealed record AssemblyExtensionSelection(
    IReadOnlyList<AssemblyExtensionDto> Items,
    int Total,
    bool Truncated);

internal sealed record AssemblyIdentityDto(
    string Name,
    string Version,
    string Culture,
    string PublicKeyToken);

internal sealed record AssemblyReferenceDto(
    string Name,
    string Version,
    string Culture,
    bool Resolved);

internal sealed record AssemblyMemberDto(
    string Kind,
    string Name,
    string Accessibility,
    string Signature,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Attributes);

internal sealed record AssemblyTypeDto(
    string Namespace,
    string Name,
    string Kind,
    string Accessibility,
    IReadOnlyList<AssemblyMemberDto> Members,
    IReadOnlyList<string> Attributes);

internal sealed record AssemblyExtensionDto(
    string Namespace,
    string DeclaringType,
    string Name,
    string Signature,
    string ReceiverType,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<string> Constraints,
    string Applicability,
    string? ApplicabilityReason,
    IReadOnlyList<string> Attributes);

internal sealed record InspectAssemblyPayload(
    string AssemblyPath,
    AssemblyIdentityDto? Identity,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<AssemblyTypeDto> Types,
    IReadOnlyList<string> Diagnostics,
    string Completeness,
    bool Truncated,
    int TotalTypes);

internal sealed record FindAssemblyExtensionsPayload(
    string AssemblyPath,
    IReadOnlyList<AssemblyExtensionDto> Extensions,
    IReadOnlyList<string> Diagnostics,
    string Completeness,
    bool Truncated,
    int TotalExtensions,
    string? ConsumerProject,
    string? ReceiverType);
