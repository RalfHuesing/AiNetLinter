#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisContextFactory
{
    internal static async Task<(AssemblyContext? Context, string? Error)> CreateAsync(
        string assemblyPath,
        Solution? consumerSolution,
        string? receiverType,
        CancellationToken ct)
    {
        var metadata = ReadMetadata(assemblyPath);
        if (metadata.Error is not null) return (null, metadata.Error);

        var diagnostics = new List<string>(metadata.Diagnostics);
        var consumer = consumerSolution is null
            ? new ConsumerSelection(null, null, null)
            : await FindConsumerCompilationAsync(consumerSolution, assemblyPath, metadata.Identity, receiverType, diagnostics, ct);
        var compilation = consumer.Compilation is null
            ? CreateStandaloneCompilation(assemblyPath, metadata.References, diagnostics)
            : AddAssemblyDirectoryDependencies(consumer.Compilation, assemblyPath, metadata.References, diagnostics);
        var targetReference = FindTargetReference(compilation, assemblyPath, metadata.Identity);
        if (targetReference is null)
        {
            try
            {
                targetReference = MetadataReference.CreateFromFile(assemblyPath);
                compilation = compilation.AddReferences(targetReference);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
            {
                return (null, $"Assembly konnte nicht als Roslyn-Metadatenreferenz geöffnet werden: {ex.Message}");
            }
        }

        var assembly = compilation.GetAssemblyOrModuleSymbol(targetReference) as IAssemblySymbol;
        if (assembly is null) return (null, "Die angegebene Datei konnte nicht als .NET-Assemblysymbol aufgelöst werden.");

        var identity = ToIdentityDto(assembly.Identity);

        diagnostics.AddRange(GetCompilationDiagnostics(compilation));
        var resolvedIdentities = compilation.References
            .Select(reference => compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol)
            .Where(symbol => symbol is not null)
            .Select(symbol => symbol!.Identity)
            .ToList();
        var references = metadata.References
            .Select(reference => reference with
            {
                Resolved = resolvedIdentities.Any(identity => IsSameIdentity(identity, reference)),
            })
            .ToList();
        diagnostics.AddRange(references
            .Where(reference => !reference.Resolved)
            .Select(reference => $"Abhängigkeit nicht auflösbar: {reference.Name}, Version {reference.Version}."));

        return (new AssemblyContext(
            assembly,
            identity,
            references,
            DistinctDiagnostics(diagnostics),
            compilation,
            consumer.Receiver,
            consumer.ProjectName), null);
    }

    private static async Task<ConsumerSelection> FindConsumerCompilationAsync(
        Solution solution,
        string assemblyPath,
        AssemblyIdentityDto? targetIdentity,
        string? receiverType,
        List<string> diagnostics,
        CancellationToken ct)
    {
        Compilation? fallback = null;
        string? fallbackName = null;
        foreach (var project in solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            Compilation? compilation;
            try
            {
                compilation = await project.GetCompilationAsync(ct);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                diagnostics.Add($"Consumer-Compilation '{project.Name}' konnte nicht geladen werden: {ex.Message}");
                continue;
            }

            if (compilation is null) continue;
            var receiver = ResolveReceiver(compilation, receiverType);
            if (receiver is not null)
            {
                return new ConsumerSelection(EnsureTargetReference(compilation, assemblyPath, targetIdentity, diagnostics), receiver, project.Name);
            }

            if (fallback is null)
            {
                fallback = EnsureTargetReference(compilation, assemblyPath, targetIdentity, diagnostics);
                fallbackName = project.Name;
            }
        }

        if (receiverType is not null)
        {
            diagnostics.Add($"Consumer-Typ '{receiverType}' konnte in keiner geladenen Compilation aufgelöst werden.");
        }

        return new ConsumerSelection(fallback, null, fallbackName);
    }

    private static Compilation EnsureTargetReference(
        Compilation compilation,
        string assemblyPath,
        AssemblyIdentityDto? metadataIdentity,
        List<string> diagnostics)
    {
        if (FindTargetReference(compilation, assemblyPath, metadataIdentity) is not null) return compilation;
        try
        {
            return compilation.AddReferences(MetadataReference.CreateFromFile(assemblyPath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
        {
            diagnostics.Add($"Assemblyreferenz konnte nicht ergänzt werden: {ex.Message}");
            return compilation;
        }
    }

    private static Compilation CreateStandaloneCompilation(
        string assemblyPath,
        IReadOnlyList<AssemblyReferenceDto> assemblyReferences,
        List<string> diagnostics)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(assemblyPath) };
        var directory = Path.GetDirectoryName(assemblyPath);
        var trustedPlatformAssemblies = GetTrustedPlatformAssemblyPaths();
        foreach (var reference in assemblyReferences)
        {
            var candidate = FindReferencePath(directory, reference.Name, trustedPlatformAssemblies);
            if (candidate is not null) paths.Add(candidate);
        }

        var references = new List<MetadataReference>();
        foreach (var path in paths.OrderBy(path => string.Equals(path, assemblyPath, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
            {
                diagnostics.Add($"Referenz konnte nicht geladen werden: {path}: {ex.Message}");
            }
        }

        return CSharpCompilation.Create(
            "AiNetLinter.AssemblyMetadataInspection",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static Compilation AddAssemblyDirectoryDependencies(
        Compilation compilation,
        string assemblyPath,
        IReadOnlyList<AssemblyReferenceDto> assemblyReferences,
        List<string> diagnostics)
    {
        var directory = Path.GetDirectoryName(assemblyPath);
        var trustedPlatformAssemblies = GetTrustedPlatformAssemblyPaths();
        foreach (var reference in assemblyReferences)
        {
            if (compilation.References
                .Select(reference => compilation.GetAssemblyOrModuleSymbol(reference) as IAssemblySymbol)
                .Any(symbol => symbol is not null && IsSameIdentity(symbol.Identity, reference)))
            {
                continue;
            }

            var candidate = FindReferencePath(directory, reference.Name, trustedPlatformAssemblies);
            if (candidate is null || compilation.References
                .OfType<PortableExecutableReference>()
                .Any(existing => existing.FilePath is not null && string.Equals(
                    Path.GetFullPath(existing.FilePath),
                    candidate,
                    StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                compilation = compilation.AddReferences(MetadataReference.CreateFromFile(candidate));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or ArgumentException)
            {
                diagnostics.Add($"Referenz konnte nicht geladen werden: {candidate}: {ex.Message}");
            }
        }

        return compilation;
    }

    private static MetadataReference? FindTargetReference(
        Compilation compilation,
        string assemblyPath,
        AssemblyIdentityDto? targetIdentity)
    {
        var exactPathReference = compilation.References
            .OfType<PortableExecutableReference>()
            .FirstOrDefault(reference => reference.FilePath is not null && string.Equals(
                Path.GetFullPath(reference.FilePath ?? string.Empty),
                Path.GetFullPath(assemblyPath),
                StringComparison.OrdinalIgnoreCase));

        if (exactPathReference is not null) return exactPathReference;
        if (targetIdentity is null) return null;

        return compilation.References
            .FirstOrDefault(reference => compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol symbol
                && IsSameIdentity(symbol.Identity, targetIdentity));
    }

    private static IReadOnlyList<string> GetCompilationDiagnostics(Compilation compilation) =>
        compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => $"{diagnostic.Id}: {diagnostic.GetMessage()}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(50)
            .ToList();

    private static IReadOnlyList<string> DistinctDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(50).ToList();

    private static ITypeSymbol? ResolveReceiver(Compilation compilation, string? receiverType)
    {
        if (string.IsNullOrWhiteSpace(receiverType)) return null;
        var normalized = receiverType.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        return compilation.GetTypeByMetadataName(normalized)
            ?? AssemblyAnalysisSymbolTraversal.GetAllTypes(compilation.GlobalNamespace).FirstOrDefault(type => string.Equals(type.ToDisplayString(), normalized, StringComparison.Ordinal));
    }

    private static MetadataReadResult ReadMetadata(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata) return new MetadataReadResult(null, Array.Empty<AssemblyReferenceDto>(), Array.Empty<string>(), "Die Datei enthält keine .NET-Metadaten.");
            var reader = peReader.GetMetadataReader();
            var definition = reader.GetAssemblyDefinition();
            var identity = new AssemblyIdentityDto(
                reader.GetString(definition.Name),
                definition.Version.ToString(),
                definition.Culture.IsNil ? "neutral" : reader.GetString(definition.Culture),
                string.Empty);
            var references = reader.AssemblyReferences
                .Select(handle => reader.GetAssemblyReference(handle))
                .Select(reference => new AssemblyReferenceDto(
                    reader.GetString(reference.Name),
                    reference.Version.ToString(),
                    reference.Culture.IsNil ? "neutral" : reader.GetString(reference.Culture),
                    false))
                .OrderBy(reference => reference.Name, StringComparer.Ordinal)
                .ThenBy(reference => reference.Version, StringComparer.Ordinal)
                .ToList();
            return new MetadataReadResult(identity, references, Array.Empty<string>(), null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return new MetadataReadResult(null, Array.Empty<AssemblyReferenceDto>(), Array.Empty<string>(), $"Assembly-Metadaten konnten nicht gelesen werden: {ex.Message}");
        }
    }

    private static AssemblyIdentityDto ToIdentityDto(AssemblyIdentity identity) =>
        new(
            identity.Name,
            identity.Version.ToString(),
            string.IsNullOrEmpty(identity.CultureName) ? "neutral" : identity.CultureName,
            identity.PublicKeyToken.IsDefaultOrEmpty ? string.Empty : Convert.ToHexString(identity.PublicKeyToken.ToArray()));

    private static bool IsSameIdentity(Microsoft.CodeAnalysis.AssemblyIdentity identity, AssemblyIdentityDto expected) =>
        IsSameIdentity(identity, expected.Name, expected.Version, expected.Culture);

    private static bool IsSameIdentity(Microsoft.CodeAnalysis.AssemblyIdentity identity, AssemblyReferenceDto expected) =>
        IsSameIdentity(identity, expected.Name, expected.Version, expected.Culture);

    private static bool IsSameIdentity(
        Microsoft.CodeAnalysis.AssemblyIdentity identity,
        string expectedName,
        string expectedVersion,
        string expectedCulture) =>
        string.Equals(identity.Name, expectedName, StringComparison.Ordinal)
        && identity.Version.ToString() == expectedVersion
        && string.Equals(
            string.IsNullOrEmpty(identity.CultureName) ? "neutral" : identity.CultureName,
            expectedCulture,
            StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> GetTrustedPlatformAssemblyPaths() =>
        AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trustedPlatformAssemblies
            ? trustedPlatformAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

    private static string? FindReferencePath(
        string? directory,
        string referenceName,
        IReadOnlyList<string> trustedPlatformAssemblies)
    {
        if (directory is not null && Directory.Exists(directory))
        {
            try
            {
                var localCandidate = Directory.EnumerateFiles(directory, "*.dll")
                    .Where(path => string.Equals(
                        Path.GetFileNameWithoutExtension(path),
                        referenceName,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (localCandidate is not null) return Path.GetFullPath(localCandidate);
            }
            catch (Exception ignored) when (ignored is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or PathTooLongException)
            {
                // Fall back to trusted platform assemblies when the target directory cannot be enumerated.
            }
        }

        return trustedPlatformAssemblies
            .Where(path => string.Equals(
                Path.GetFileNameWithoutExtension(path),
                referenceName,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private sealed record ConsumerSelection(Compilation? Compilation, ITypeSymbol? Receiver, string? ProjectName);

    private sealed record MetadataReadResult(
        AssemblyIdentityDto? Identity,
        IReadOnlyList<AssemblyReferenceDto> References,
        IReadOnlyList<string> Diagnostics,
        string? Error);
}
