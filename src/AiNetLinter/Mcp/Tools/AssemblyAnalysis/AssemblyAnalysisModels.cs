#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Registration;
using AiNetLinter.Mcp.Tools.SymbolGraph;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal sealed record InspectAssemblyArguments(
    string? AssemblyPath,
    string? Namespace,
    string? TypeName,
    string? MemberName,
    bool PublicOnly,
    int MaxResults,
    bool ExactTypeName = false,
    IReadOnlyList<string>? MemberNames = null,
    int MaxMembers = 0,
    bool? IncludeReferences = null,
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null)
{
    internal bool IncludeReferenceDetails => IncludeReferences ?? (
        string.IsNullOrWhiteSpace(TypeName)
        && string.IsNullOrWhiteSpace(MemberName)
        && (MemberNames is null || MemberNames.All(string.IsNullOrWhiteSpace)));
}

internal sealed record FindAssemblyExtensionsArguments(
    string? AssemblyPath,
    string? ReceiverType,
    string? ExtensionName,
    string? Namespace,
    int MaxResults,
    bool IncludeReferences = false,
    int MaxResponseBytes = 0,
    string? DetailLevel = null,
    string? Cursor = null);

internal sealed record AssemblyInspectionOptions(
    string? NamespaceFilter,
    string? TypeFilter,
    string? MemberFilter,
    bool PublicOnly,
    bool ExactTypeName,
    IReadOnlyList<string>? MemberNames,
    int MaxResults,
    int MaxMembers,
    int Offset = 0,
    AnalysisSymbolIdentity? HandoffIdentity = null);

internal sealed record AssemblyExtensionSearchOptions(
    string? ExtensionName,
    string? NamespaceFilter,
    string? ReceiverType,
    int MaxResults,
    int Offset = 0,
    AnalysisSymbolIdentity? HandoffIdentity = null);

internal sealed record AssemblyTypeSelection(
    IReadOnlyList<AssemblyTypeDto> Items,
    IReadOnlyList<string> Namespaces,
    int Total,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy);

internal sealed record AssemblyExtensionSelection(
    IReadOnlyList<AssemblyExtensionDto> Items,
    int Total,
    bool Truncated,
    IReadOnlyList<string> TruncatedBy);

internal sealed record AssemblyIdentityDto(
    string Name,
    string Version,
    string Culture,
    string PublicKeyToken);

internal sealed record AssemblyReferenceDto(
    string Name,
    string Version,
    string Culture,
    bool Resolved,
    string? ResolvedPath = null,
    string ResolutionState = "resolved",
    int Depth = 1,
    string? Diagnostic = null,
    string? SourceProjectPath = null);

internal sealed record AssemblyReferenceSessionDto(
    AssemblyReferenceDto Reference,
    string AssemblyPath,
    AssemblyIdentityDto? Identity,
    IReadOnlyList<string> Diagnostics,
    string Completeness,
    AssemblyOrigin? Origin = null,
    string SessionStatus = "complete",
    AssemblyDiagnosticSummary? DiagnosticsSummary = null);

internal sealed record AssemblyMemberDto(
    string Kind,
    string Name,
    string Accessibility,
    string Signature,
    IReadOnlyList<AssemblyParameterDto> Parameters,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<string> Attributes,
    string? Id = null,
    bool Handoff = false,
    IReadOnlyList<string>? AllowedFollowUpTools = null);

internal sealed record AssemblyParameterDto(
    string Name,
    string Type,
    string RefKind,
    bool IsOptional,
    string? DefaultValue);

internal sealed record AssemblyTypeDto(
    string Namespace,
    string Name,
    string Kind,
    string Accessibility,
    IReadOnlyList<AssemblyMemberDto> Members,
    IReadOnlyList<string> Attributes,
    int TotalMembers = 0,
    bool MembersTruncated = false,
    IReadOnlyList<string>? TruncatedBy = null,
    string? Id = null,
    bool Handoff = false,
    IReadOnlyList<string>? AllowedFollowUpTools = null);

internal sealed record AssemblyExtensionDto(
    string Namespace,
    string DeclaringType,
    string Name,
    string Signature,
    string ReceiverType,
    IReadOnlyList<string> GenericParameters,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<AssemblyParameterDto> Parameters,
    string Applicability,
    string? ApplicabilityReason,
    IReadOnlyList<string> Attributes,
    string? Id = null);

internal sealed record InspectAssemblyPayload(
    string AssemblyPath,
    AssemblyIdentityDto? Identity,
    IReadOnlyList<string> Namespaces,
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlyList<AssemblyTypeDto> Types,
    IReadOnlyList<string> Diagnostics,
    string Completeness,
    bool Truncated,
    int TotalTypes,
    int ShownCount,
    IReadOnlyList<string> TruncatedBy,
    AssemblyOrigin? Origin = null,
    long Generation = 0,
    string SessionStatus = "complete",
    IReadOnlyList<AssemblyReferenceSessionDto>? ReferenceSessions = null,
    AssemblyDiagnosticsSummary? DiagnosticsSummary = null,
    AssemblyReferenceSummary? ReferenceSummary = null,
    bool ReferenceDetailsIncluded = true,
    int TotalNamespaces = 0,
    string? DecompiledProjectDirectory = null,
    string? DecompiledProjectPath = null,
    string? DecompiledSourceRoot = null,
    int TotalCount = 0,
    int ReturnedCount = 0,
    bool IsTruncated = false,
    string? ContinuationToken = null,
    string Scope = "root")
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpNavigationPayload? Navigation { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal sealed record FindAssemblyExtensionsPayload(
    string AssemblyPath,
    IReadOnlyList<AssemblyExtensionDto> Extensions,
    IReadOnlyList<string> Diagnostics,
    string Completeness,
    bool Truncated,
    int TotalExtensions,
    int ShownCount,
    IReadOnlyList<string> TruncatedBy,
    string? ConsumerProject,
    string? ReceiverType,
    AssemblyOrigin? Origin = null,
    long Generation = 0,
    string SessionStatus = "complete",
    IReadOnlyList<AssemblyReferenceDto>? References = null,
    IReadOnlyList<AssemblyReferenceSessionDto>? ReferenceSessions = null,
    AssemblyDiagnosticsSummary? DiagnosticsSummary = null,
    AssemblyReferenceSummary? ReferenceSummary = null,
    int TotalCount = 0,
    int ReturnedCount = 0,
    bool IsTruncated = false,
    string? ContinuationToken = null,
    string Scope = "root")
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpNavigationPayload? Navigation { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

internal static class AssemblyPaging
{
    internal static int ReadOffset(string? cursor) =>
        TryReadUnboundOffset(cursor, out var offset) ? offset : 0;

    internal static string CreateToken(int offset) => offset.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal static string CreateToken(int offset, string binding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binding);
        return $"v1.{Math.Max(0, offset).ToString(System.Globalization.CultureInfo.InvariantCulture)}.{binding}";
    }

    internal static string CreateBinding(string canonicalPath, string contentHash, params string?[] queryParts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        var material = string.Join("\u001f", new[] { canonicalPath, contentHash }.Concat(queryParts));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    internal static string CreateInspectBinding(string canonicalPath, string contentHash, InspectAssemblyArguments arguments) =>
        CreateBinding(
            canonicalPath,
            contentHash,
            "inspect_assembly",
            arguments.Namespace,
            arguments.TypeName,
            arguments.MemberName,
            arguments.PublicOnly.ToString(),
            arguments.MaxResults.ToString(),
            arguments.ExactTypeName.ToString(),
            arguments.MemberNames is null ? null : string.Join("\u001e", arguments.MemberNames),
            arguments.MaxMembers.ToString(),
            arguments.IncludeReferences?.ToString(),
            arguments.DetailLevel);

    internal static string CreateExtensionsBinding(
        string canonicalPath,
        string contentHash,
        FindAssemblyExtensionsArguments arguments) =>
        CreateBinding(
            canonicalPath,
            contentHash,
            "find_assembly_extensions",
            arguments.ReceiverType,
            arguments.ExtensionName,
            arguments.Namespace,
            arguments.MaxResults.ToString(),
            arguments.IncludeReferences.ToString(),
            arguments.DetailLevel);

    internal static string CreateSearchBinding(
        string canonicalPath,
        string contentHash,
        AssemblySearchArguments arguments) =>
        CreateBinding(
            canonicalPath,
            contentHash,
            "search_assembly",
            arguments.Pattern,
            arguments.IsRegex?.ToString(),
            arguments.SearchKind,
            arguments.MaxResults.ToString(),
            arguments.MaxFiles.ToString(),
            arguments.ContextLines.ToString(),
            arguments.FileFilter,
            arguments.DeclarationOnly.ToString(),
            arguments.Kind);

    internal static bool TryReadBoundOffset(string? cursor, string binding, out int offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(cursor)) return true;

        var parts = cursor.Split('.', 3, StringSplitOptions.None);
        if (parts.Length != 3
            || !string.Equals(parts[0], "v1", StringComparison.Ordinal)
            || !int.TryParse(parts[1], out offset)
            || offset < 0
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(parts[2]),
                Encoding.ASCII.GetBytes(binding)))
        {
            offset = 0;
            return false;
        }

        return true;
    }

    internal static string? FindBinding(JsonNode? node)
    {
        return node switch
        {
            JsonObject obj => FindBindingInObject(obj),
            JsonArray array => FindBindingInArray(array),
            _ => null,
        };
    }

    private static string? FindBindingInObject(JsonObject obj)
    {
        foreach (var property in obj)
        {
            if (TryGetTokenBinding(property.Key, property.Value, out var binding)) return binding;
            var nested = FindBinding(property.Value);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static string? FindBindingInArray(JsonArray array)
    {
        foreach (var item in array)
        {
            var nested = FindBinding(item);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static bool TryGetTokenBinding(string propertyName, JsonNode? value, out string? binding)
    {
        binding = null;
        return propertyName.EndsWith("ContinuationToken", StringComparison.OrdinalIgnoreCase)
            && value is JsonValue jsonValue
            && jsonValue.TryGetValue<string>(out var token)
            && TryReadTokenParts(token, out _, out binding);
    }

    internal static void RebindContinuationTokens(JsonNode? node, string? binding)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Key.EndsWith("ContinuationToken", StringComparison.OrdinalIgnoreCase)
                    && property.Value is JsonValue value
                    && value.TryGetValue<string>(out var token)
                    && int.TryParse(token, out var offset)
                    && binding is not null)
                {
                    obj[property.Key] = CreateToken(offset, binding);
                }
                else
                {
                    RebindContinuationTokens(property.Value, binding);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) RebindContinuationTokens(item, binding);
        }
    }

    private static bool TryReadUnboundOffset(string? cursor, out int offset)
    {
        if (int.TryParse(cursor?.Trim(), out offset) && offset >= 0) return true;

        var parts = cursor?.Split('.', 3, StringSplitOptions.None);
        return parts is { Length: 3 }
            && string.Equals(parts[0], "v1", StringComparison.Ordinal)
            && int.TryParse(parts[1], out offset)
            && offset >= 0;
    }

    private static bool TryReadTokenParts(string? token, out int offset, out string? binding)
    {
        offset = 0;
        binding = null;
        var parts = token?.Split('.', 3, StringSplitOptions.None);
        if (parts is not { Length: 3 }
            || !string.Equals(parts[0], "v1", StringComparison.Ordinal)
            || !int.TryParse(parts[1], out offset)
            || offset < 0
            || string.IsNullOrWhiteSpace(parts[2])) return false;

        binding = parts[2];
        return true;
    }
}
