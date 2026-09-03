#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using AiNetLinter.Mcp.Assemblies.Analysis.References;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyReferenceResolver
{
    private const string LegacyCoreLibraryName = "mscorlib";
    private const string FrameworkBaseLibraryName = "System";
    private static readonly string[] VersionTolerantFrameworkPrefixes =
    [
        "System.",
        "Microsoft.",
        "WindowsBase",
    ];

    internal const int MaxReferenceDepth = 8;
    internal const int MaxReferenceNodes = 128;
    internal const string BoundaryDiagnosticCode = "assembly-reference-boundary";
    internal const string NativeMetadataFailureMessage = "Die Datei enthält keine .NET-Metadaten.";

    internal AssemblyReferenceResolution Resolve(string assemblyPath)
    {
        var canonicalPath = AssemblyFingerprintCalculator.Canonicalize(assemblyPath);
        try
        {
            using var stream = File.OpenRead(canonicalPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return FailedResolution(AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceResolver.Resolve)), NativeMetadataFailureMessage);
            }
            var diagnostics = new List<AssemblySessionDiagnostic>();
            var metadata = ReadMetadata(peReader.GetMetadataReader());
            var graph = BuildReferenceGraph(canonicalPath, metadata, diagnostics);
            var metadataResult = CreateMetadataReferences(graph.Paths, diagnostics);
            var references = graph.References.Select(reference => NormalizeReference(reference, metadataResult.SuccessfulPaths)).ToList();
            return new AssemblyReferenceResolution(metadata.Identity, references, metadataResult.References, diagnostics);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return FailedResolution(AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceResolution.Identity)), $"Assembly-Metadaten konnten nicht gelesen werden: {ex.Message}");
        }
    }

    internal SourceProjectReferenceResolution ResolveSourceProjectReferences(
        Project rootProject,
        Solution solution,
        IReadOnlyList<AssemblyReferenceDto> existingReferences)
    {
        ArgumentNullException.ThrowIfNull(rootProject);
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(existingReferences);

        return new SourceProjectReferenceGraph(solution, existingReferences).Resolve(rootProject);
    }

    private static ReferenceGraph BuildReferenceGraph(
        string canonicalPath,
        AssemblyMetadata metadata,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var trustedPaths = GetTrustedPlatformAssemblyPaths();
        var graph = new ReferenceGraph(canonicalPath, metadata);
        VisitNode(canonicalPath, trustedPaths, graph, diagnostics);
        return graph;
    }

    private static void VisitNode(
        string path,
        IReadOnlyList<string> trustedPaths,
        ReferenceGraph graph,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        if (graph.Visited.Count >= MaxReferenceNodes) return;
        var node = graph.Nodes[path];
        foreach (var reference in node.Metadata.References)
        {
            var resolution = FindReferencePath(reference, Path.GetDirectoryName(node.Path), trustedPaths, diagnostics);
            var candidate = CreateCandidate(node, reference, resolution, graph, diagnostics);
            if (!graph.TryAdd(candidate)) continue;
            if (candidate.ResolutionState is not "resolved" || candidate.ResolvedPath is null) continue;
            if (trustedPaths.Contains(candidate.ResolvedPath, StringComparer.OrdinalIgnoreCase))
            {
                graph.AddPath(candidate.ResolvedPath);
                continue;
            }

            VisitChild(candidate, node, trustedPaths, graph, diagnostics);
        }
    }

    private static void VisitChild(
        AssemblyReferenceDto candidate,
        ReferenceNode parent,
        IReadOnlyList<string> trustedPaths,
        ReferenceGraph graph,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        if (graph.Visited.Count >= MaxReferenceNodes)
        {
            diagnostics.Add(new(BoundaryDiagnosticCode, $"Die Referenzauflösung erreicht die Begrenzung von {MaxReferenceNodes} Assemblies.", AssemblyDiagnosticSeverity.Warning));
            return;
        }

        if (!TryReadMetadata(candidate.ResolvedPath!, out var metadata, diagnostics))
        {
            graph.ReplaceLast(candidate with
            {
                Resolved = false,
                ResolvedPath = null,
                ResolutionState = "invalid",
                Diagnostic = $"Metadaten von '{candidate.ResolvedPath}' konnten nicht gelesen werden.",
            });
            return;
        }

        graph.AddPath(candidate.ResolvedPath!);
        var ancestors = new HashSet<string>(parent.Ancestors, StringComparer.OrdinalIgnoreCase) { candidate.ResolvedPath! };
        graph.Nodes.Add(candidate.ResolvedPath!, new ReferenceNode(candidate.ResolvedPath!, metadata, candidate.Depth, ancestors));
        VisitNode(candidate.ResolvedPath!, trustedPaths, graph, diagnostics);
    }

    private static AssemblyReferenceDto CreateCandidate(
        ReferenceNode node,
        AssemblyReferenceDto reference,
        ReferencePathResolution resolution,
        ReferenceGraph graph,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var state = DetermineState(node, resolution, graph);
        var diagnostic = resolution.Diagnostic;
        if (state is "depth_limit" or "cycle")
        {
            diagnostic = state switch
            {
                "depth_limit" => $"Referenz '{reference.Name}' überschreitet die maximale Referenztiefe {MaxReferenceDepth}.",
                "cycle" => $"Zyklische Referenz erkannt: '{reference.Name}' verweist auf '{resolution.Path}'.",
                _ => null,
            };
            diagnostics.Add(new(state == "cycle" ? "assembly-reference-cycle" : BoundaryDiagnosticCode, diagnostic!, AssemblyDiagnosticSeverity.Warning));
        }

        return reference with
        {
            Resolved = resolution.Path is not null && state is ("resolved" or "cycle" or "deduplicated"),
            ResolvedPath = resolution.Path,
            ResolutionState = state,
            Depth = node.Depth + 1,
            Diagnostic = diagnostic,
        };
    }

    private static string DetermineState(ReferenceNode node, ReferencePathResolution resolution, ReferenceGraph graph) =>
        resolution.Path is null
            ? resolution.State
            : node.Depth >= MaxReferenceDepth
                ? "depth_limit"
                : node.Ancestors.Contains(resolution.Path)
                    ? "cycle"
                    : graph.Visited.Contains(resolution.Path)
                        ? "deduplicated"
                        : "resolved";

    private static AssemblyReferenceDto NormalizeReference(
        AssemblyReferenceDto reference,
        IReadOnlySet<string> successfulPaths) =>
        reference with
        {
            Resolved = reference.Resolved && reference.ResolvedPath is not null && successfulPaths.Contains(reference.ResolvedPath),
            ResolvedPath = reference.Resolved && reference.ResolvedPath is not null && successfulPaths.Contains(reference.ResolvedPath)
                ? reference.ResolvedPath
                : null,
        };

    private static ReferencePathResolution FindReferencePath(
        AssemblyReferenceDto reference,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var candidates = EnumerateCandidatePaths(reference.Name, directory, trustedPlatformAssemblies, diagnostics);
        var mismatches = new List<string>();
        foreach (var candidate in candidates)
        {
            if (!TryReadIdentity(candidate, out var identity, diagnostics)) continue;
            if (IdentityMatches(reference, identity)) return new(candidate, "resolved", null);
            mismatches.Add($"{candidate} ({identity.Version}, {identity.Culture})");
        }

        if (mismatches.Count > 0)
        {
            var diagnostic = $"Kein identitätsgleicher Kandidat für '{reference.Name}' gefunden. Erwartet: Version {reference.Version}, Kultur {reference.Culture}; geprüft: {string.Join(", ", mismatches.Take(5))}.";
            diagnostics.Add(new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Version)),
                diagnostic,
                AssemblyDiagnosticSeverity.Warning));
            return new(null, "version_mismatch", diagnostic);
        }

        var missing = $"Abhängigkeit nicht auflösbar: {reference.Name}, Version {reference.Version}, Kultur {reference.Culture}.";
        diagnostics.Add(new(
            AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Resolved)),
            missing,
            AssemblyDiagnosticSeverity.Warning));
        return new(null, "missing", missing);
    }

    private static bool TryReadMetadata(
        string path,
        out AssemblyMetadata metadata,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) throw new BadImageFormatException("Keine .NET-Metadaten vorhanden.");
            metadata = ReadMetadata(peReader.GetMetadataReader());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyIdentityDto)),
                $"Referenzkandidat konnte nicht statisch geprüft werden: {path}: {ex.Message}",
                AssemblyDiagnosticSeverity.Warning));
            metadata = null!;
            return false;
        }
    }

    private static IReadOnlyList<string> EnumerateCandidatePaths(
        string referenceName,
        string? directory,
        IReadOnlyList<string> trustedPlatformAssemblies,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var candidates = new List<string>();
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                candidates.AddRange(Directory.EnumerateFiles(directory, referenceName + ".dll", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFullPath)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                diagnostics.Add(new(
                    AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyReferenceDto.Name)),
                    $"Lokale Referenzen konnten nicht enumeriert werden: {directory}: {ex.Message}",
                    AssemblyDiagnosticSeverity.Warning));
            }
        }

        candidates.AddRange(trustedPlatformAssemblies
            .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), referenceName, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static MetadataReferenceResult CreateMetadataReferences(
        IEnumerable<string> paths,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var references = new List<MetadataReference>();
        var successfulPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
                successfulPaths.Add(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException or InvalidOperationException)
            {
                diagnostics.Add(new(AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(Microsoft.CodeAnalysis.MetadataReference)), $"Referenz konnte nicht geladen werden: {path}: {ex.Message}", AssemblyDiagnosticSeverity.Warning));
            }
        }

        return new MetadataReferenceResult(references, successfulPaths);
    }

    private static bool TryReadIdentity(
        string path,
        out AssemblyIdentityDto identity,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) throw new BadImageFormatException("Keine .NET-Metadaten vorhanden.");
            identity = ReadIdentity(peReader.GetMetadataReader());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            diagnostics.Add(new(AssemblyDiagnosticCodes.For(nameof(AssemblyReferenceResolver), nameof(AssemblyIdentityDto)), $"Referenzkandidat konnte nicht statisch geprüft werden: {path}: {ex.Message}", AssemblyDiagnosticSeverity.Warning));
            identity = null!;
            return false;
        }
    }

    private static AssemblyMetadata ReadMetadata(MetadataReader reader)
    {
        var identity = ReadIdentity(reader);
        var references = reader.AssemblyReferences
            .Select(handle => reader.GetAssemblyReference(handle))
            .Select(reference => new AssemblyReferenceDto(
                reader.GetString(reference.Name),
                reference.Version.ToString(),
                NormalizeCulture(reference.Culture.IsNil ? "neutral" : reader.GetString(reference.Culture)),
                false))
            .OrderBy(reference => reference.Name, StringComparer.Ordinal)
            .ThenBy(reference => reference.Version, StringComparer.Ordinal)
            .ToList();
        return new AssemblyMetadata(identity, references);
    }

    private static AssemblyIdentityDto ReadIdentity(MetadataReader reader)
    {
        var definition = reader.GetAssemblyDefinition();
        return new AssemblyIdentityDto(
            reader.GetString(definition.Name),
            definition.Version.ToString(),
            NormalizeCulture(definition.Culture.IsNil ? "neutral" : reader.GetString(definition.Culture)),
            GetPublicKeyToken(reader, definition));
    }

    private static string GetPublicKeyToken(MetadataReader reader, AssemblyDefinition definition)
    {
        if (definition.PublicKey.IsNil) return string.Empty;
        var hash = SHA1.HashData(reader.GetBlobBytes(definition.PublicKey));
        var token = hash[^8..].Reverse().ToArray();
        return Convert.ToHexString(token);
    }

    internal static bool IdentityMatches(AssemblyReferenceDto expected, AssemblyIdentityDto actual) =>
        string.Equals(expected.Name, actual.Name, StringComparison.OrdinalIgnoreCase)
        && (IsVersionTolerantFrameworkAssembly(expected.Name)
            || string.Equals(expected.Version, actual.Version, StringComparison.Ordinal))
        && string.Equals(NormalizeCulture(expected.Culture), NormalizeCulture(actual.Culture), StringComparison.OrdinalIgnoreCase);

    private static bool IsVersionTolerantFrameworkAssembly(string name) =>
        string.Equals(name, LegacyCoreLibraryName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, FrameworkBaseLibraryName, StringComparison.OrdinalIgnoreCase)
        || VersionTolerantFrameworkPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeCulture(string culture) =>
        string.IsNullOrWhiteSpace(culture) || string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase)
            ? "neutral"
            : culture.Trim().ToLowerInvariant();

    private static AssemblyReferenceResolution FailedResolution(string code, string message)
    {
        message = $"{message} Hinweis: verwaltete .NET-.dll oder .exe mit IL erforderlich.";
        return new AssemblyReferenceResolution(null, [], [], [new AssemblySessionDiagnostic(code, message, AssemblyDiagnosticSeverity.Error)]);
    }

    private static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths() =>
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string paths
            ? paths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : [];

    private sealed record AssemblyMetadata(AssemblyIdentityDto Identity, IReadOnlyList<AssemblyReferenceDto> References);

    private sealed record ReferenceNode(
        string Path,
        AssemblyMetadata Metadata,
        int Depth,
        HashSet<string> Ancestors);

    private sealed class ReferenceGraph
    {
        private readonly HashSet<string> edgeKeys = new(StringComparer.OrdinalIgnoreCase);

        internal ReferenceGraph(string canonicalPath, AssemblyMetadata metadata)
        {
            Paths = [canonicalPath];
            Visited = new(StringComparer.OrdinalIgnoreCase) { canonicalPath };
            Nodes = new(StringComparer.OrdinalIgnoreCase)
            {
                [canonicalPath] = new ReferenceNode(canonicalPath, metadata, 0, [canonicalPath]),
            };
        }

        internal List<AssemblyReferenceDto> References { get; } = [];
        internal List<string> Paths { get; }
        internal HashSet<string> Visited { get; }
        internal Dictionary<string, ReferenceNode> Nodes { get; }

        internal void AddPath(string path)
        {
            if (Visited.Add(path)) Paths.Add(path);
        }

        internal bool TryAdd(AssemblyReferenceDto candidate)
        {
            var key = string.Join("|", candidate.Name, candidate.Version, candidate.Culture, candidate.ResolvedPath ?? candidate.ResolutionState);
            if (!edgeKeys.Add(key)) return false;
            References.Add(candidate);
            return true;
        }

        internal void ReplaceLast(AssemblyReferenceDto candidate) => References[^1] = candidate;
    }

    private sealed record ReferencePathResolution(
        string? Path,
        string State,
        string? Diagnostic);

    private sealed record MetadataReferenceResult(
        IReadOnlyList<MetadataReference> References,
        IReadOnlySet<string> SuccessfulPaths);
}

internal sealed record SourceProjectReferenceResolution(
    IReadOnlyList<AssemblyReferenceDto> References,
    IReadOnlySet<string> AssemblyNames,
    IReadOnlyList<AssemblySessionDiagnostic> Diagnostics);
