#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyDecompilationCache
{
    private const int PointerPublishAttempts = 3;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        WriteIndented = true,
    };
    static AssemblyDecompilationCache()
    {
        JsonOptions.Converters.Add(new AssemblyDecompilationManifestJsonConverter());
    }

    internal AssemblyDecompilationCache(string? cacheRoot = null)
    {
        RootPath = Path.GetFullPath(cacheRoot ?? Path.Combine(AppContext.BaseDirectory, "cache", "assembly"));
    }
    internal string RootPath { get; }

    internal string GetEntryDirectory(AssemblyDecompilationCacheKey key)
    {
        var pathHash = HashSegment(key.CanonicalPath, 16);
        var keyHash = HashSegment(key.StableValue, 32);
        return Path.Combine(RootPath, pathHash, keyHash);
    }

    internal bool TryRead(
        AssemblyCacheReadRequest request,
        out CachedDecompilationGeneration? generation,
        out AssemblySessionDiagnostic? diagnostic)
    {
        generation = null;
        diagnostic = null;
        var entryDirectory = GetEntryDirectory(request.Key);
        var pointerPath = Path.Combine(entryDirectory, AssemblyCacheContract.CurrentPointerFileName);
        if (!File.Exists(pointerPath)) return false;

        try
        {
            var generationDirectory = ReadPointer(entryDirectory, pointerPath);
            generation = ReadGeneration(generationDirectory, request.Key, request.Fingerprint, request.References);
            return true;
        }
        catch (Exception ex) when (IsCacheInputException(ex))
        {
            diagnostic = new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheReadRequest)),
                $"Der aktuelle Assembly-Cacheeintrag ist ungültig und wird verworfen: {ex.Message}",
                "warning");
            return false;
        }
    }

    internal AssemblyCachePublishResult Publish(AssemblyCachePublishRequest request)
    {
        var entryDirectory = GetEntryDirectory(request.CacheKey);
        var generationDirectory = Path.Combine(entryDirectory, AssemblyCacheContract.GenerationDirectoryPrefix + Guid.NewGuid().ToString("N"));
        var isPublished = false;
        try
        {
            ValidatePublishRequest(request);
            Directory.CreateDirectory(entryDirectory);
            WriteGeneration(generationDirectory, request);
            _ = ReadGeneration(generationDirectory, request.CacheKey, request.Fingerprint, request.References);

            var readRequest = new AssemblyCacheReadRequest(request.CacheKey, request.Fingerprint, request.References);
            if (TryRead(readRequest, out _, out _))
            {
                return new AssemblyCachePublishResult(true, generationDirectory, null);
            }

            if (!TryPublishPointer(entryDirectory, generationDirectory, request, out var diagnostic))
            {
                return new AssemblyCachePublishResult(false, null, diagnostic);
            }

            isPublished = true;
            return new AssemblyCachePublishResult(true, generationDirectory, null);
        }
        catch (Exception ex) when (IsCacheWriteException(ex))
        {
            return new AssemblyCachePublishResult(
                false,
                null,
                new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCachePublishRequest)), $"Assembly-Cachegeneration konnte nicht veröffentlicht werden: {ex.Message}", "error"));
        }
        finally
        {
            if (!isPublished) AssemblyCacheCleanup.TryDeleteDirectory(generationDirectory);
        }
    }

    private static void WriteGeneration(string generationDirectory, AssemblyCachePublishRequest request)
    {
        Directory.CreateDirectory(generationDirectory);
        var generatedFiles = WriteDocuments(generationDirectory, request.Decompilation.Documents);
        var manifest = CreateManifest(request, generatedFiles);
        var manifestPath = Path.Combine(generationDirectory, AssemblyCacheContract.ManifestFileName);
        WriteTextAtomically(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    private static void ValidatePublishRequest(AssemblyCachePublishRequest request)
    {
        if (request.Status is AssemblySessionStatus.Loading or AssemblySessionStatus.Failed)
        {
            throw new InvalidDataException("Nur analysierbare Assembly-Zustände dürfen im Cache veröffentlicht werden.");
        }

        if (request.Decompilation.Documents.Count == 0)
        {
            throw new InvalidDataException("Eine veröffentlichte Assembly-Generation benötigt mindestens ein Dokument.");
        }

        if (request.Status != AssemblySessionStatus.Complete) return;
        if (!request.Decompilation.IsComplete
            || request.Decompilation.Diagnostics.Any(diagnostic => !IsWarning(diagnostic))
            || request.References.References.Any(reference => !reference.Resolved))
        {
            throw new InvalidDataException("Eine vollständige Assembly-Generation darf keine Fehler oder ungelösten Referenzen enthalten.");
        }
    }

    private bool TryPublishPointer(
        string entryDirectory,
        string generationDirectory,
        AssemblyCachePublishRequest request,
        out AssemblySessionDiagnostic? diagnostic)
    {
        diagnostic = null;
        var pointerPath = Path.Combine(entryDirectory, AssemblyCacheContract.CurrentPointerFileName);
        var generationName = Path.GetFileName(generationDirectory);
        for (var attempt = 0; attempt < PointerPublishAttempts; attempt++)
        {
            var readRequest = new AssemblyCacheReadRequest(request.CacheKey, request.Fingerprint, request.References);
            if (TryRead(readRequest, out _, out _)) return true;
            var attemptResult = PublishPointerAttempt(pointerPath, generationName, readRequest);
            if (attemptResult.Succeeded) return true;
            diagnostic = attemptResult.Diagnostic;
        }

        diagnostic ??= new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCachePublishRequest)), "Current-Pointer konnte nach begrenzten Versuchen nicht validiert veröffentlicht werden.", "error");
        return false;
    }
    private PointerPublishAttempt PublishPointerAttempt(
        string pointerPath,
        string generationName,
        AssemblyCacheReadRequest readRequest)
    {
        var temporaryPointer = pointerPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WritePointer(temporaryPointer, generationName);
            ReplacePointer(pointerPath, temporaryPointer);
            var succeeded = TryRead(readRequest, out _, out _);
            return new PointerPublishAttempt(succeeded, succeeded ? null : new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheContract.CurrentPointerFileName)), "Der neu veröffentlichte Current-Pointer konnte nicht erneut validiert werden.", "warning"));
        }
        catch (IOException ex)
        {
            var diagnostic = new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheContract.CurrentPointerFileName)), $"Current-Pointer konnte nicht ersetzt werden: {ex.Message}", "warning");
            return new(TryRead(readRequest, out _, out _), diagnostic);
        }
        finally
        {
            AssemblyCacheCleanup.TryDeleteFile(temporaryPointer);
        }
    }
    private static void ReplacePointer(string pointerPath, string temporaryPointer)
    {
        if (File.Exists(pointerPath))
        {
            File.Replace(temporaryPointer, pointerPath, null, ignoreMetadataErrors: true);
            return;
        }

        if (File.Exists(pointerPath)) return;
        File.Move(temporaryPointer, pointerPath);
    }
    private static void WritePointer(string path, string generation)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, AssemblyCacheContract.FileBufferSize, FileOptions.WriteThrough);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        writer.WriteStartObject();
        writer.WriteString(nameof(generation), generation);
        writer.WriteEndObject();
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static string ReadPointer(string entryDirectory, string pointerPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(pointerPath, Utf8));
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Der Current-Pointer ist kein JSON-Objekt.");
        string? generation = null;
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, nameof(generation), StringComparison.Ordinal) || generation is not null)
            {
                throw new InvalidDataException("Der Current-Pointer enthält unerwartete oder doppelte Felder.");
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("Der Current-Pointer muss auf eine Generation verweisen.");
            }

            generation = property.Value.GetString();
        }

        if (string.IsNullOrWhiteSpace(generation)) throw new InvalidDataException("Der Current-Pointer enthält keine Generation.");
        var normalized = generation.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.Contains('/', StringComparison.Ordinal)
            || normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new InvalidDataException("Der Current-Pointer enthält einen unsicheren Generationpfad.");
        }

        var generationDirectory = ResolveSafePath(entryDirectory, normalized);
        if (!Directory.Exists(generationDirectory)) throw new InvalidDataException("Die referenzierte Cachegeneration fehlt.");
        return generationDirectory;
    }

    private static CachedDecompilationGeneration ReadGeneration(
        string generationDirectory,
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references)
    {
        var manifestPath = ResolveSafePath(generationDirectory, AssemblyCacheContract.ManifestFileName);
        var manifest = JsonSerializer.Deserialize<AssemblyDecompilationManifest>(File.ReadAllText(manifestPath, Utf8), JsonOptions)
            ?? throw new InvalidDataException("Das Cachemanifest ist leer.");
        if (!IsManifestCompatible(manifest, key, fingerprint, references))
        {
            throw new InvalidDataException("Das Cachemanifest ist inkompatibel oder statusseitig inkonsistent.");
        }

        var documents = ReadDocuments(generationDirectory, manifest);
        var updatedStatus = manifest.Status with { LastAccessUtc = DateTime.UtcNow };
        return new CachedDecompilationGeneration(manifest with { Status = updatedStatus }, documents);
    }

    private static bool IsManifestCompatible(
        AssemblyDecompilationManifest manifest,
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint,
        AssemblyReferenceResolution references)
    {
        return IsStatusCompatible(manifest.Status, manifest.Diagnostics)
            && IsInputCompatible(manifest.Input, key, fingerprint)
            && IsFormatCompatible(manifest.Format, key)
            && IsReferencesCompatible(manifest.References, manifest.Diagnostics, references);
    }

    private static bool IsStatusCompatible(AssemblyManifestStatus status, AssemblyManifestDiagnostics diagnostics)
    {
        if (!AssemblySessionStatusExtensions.TryParsePersisted(status.Status, out var parsed)) return false;
        return status.Complete == (parsed == AssemblySessionStatus.Complete)
            && (!status.Complete || (diagnostics.Errors.Count == 0 && diagnostics.UnresolvedReferences.Count == 0))
            && AreMessagesValid(diagnostics.Warnings)
            && AreMessagesValid(diagnostics.Errors)
            && AreMessagesValid(diagnostics.UnresolvedReferences)
            && status.CreatedUtc != default
            && status.LastAccessUtc != default;
    }

    private static bool AreMessagesValid(IReadOnlyList<string> messages) =>
        messages.All(message => !string.IsNullOrWhiteSpace(message));

    private static bool IsInputCompatible(
        AssemblyManifestInput input,
        AssemblyDecompilationCacheKey key,
        AssemblyFingerprint fingerprint) =>
        !string.IsNullOrWhiteSpace(input.CacheKey)
        && Path.IsPathFullyQualified(input.CanonicalPath)
        && Path.IsPathFullyQualified(input.OriginalPath)
        && string.Equals(input.OriginalPath, fingerprint.CanonicalPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(input.CacheKey, key.StableValue, StringComparison.Ordinal)
        && string.Equals(input.CanonicalPath, fingerprint.CanonicalPath, StringComparison.OrdinalIgnoreCase)
        && string.Equals(input.Sha256, fingerprint.Sha256, StringComparison.OrdinalIgnoreCase)
        && input.Length >= 0
        && input.Length == fingerprint.Length;

    private static bool IsFormatCompatible(AssemblyManifestFormat format, AssemblyDecompilationCacheKey key) =>
        string.Equals(format.DecompilerVersion, key.DecompilerVersion, StringComparison.Ordinal)
        && string.Equals(format.OptionsIdentity, key.OptionsIdentity, StringComparison.Ordinal)
        && string.Equals(format.CacheSchemaVersion, key.CacheSchemaVersion, StringComparison.Ordinal)
        && string.Equals(format.Encoding, AssemblyCacheContract.Utf8EncodingName, StringComparison.OrdinalIgnoreCase);

    private static bool IsReferencesCompatible(
        AssemblyManifestReferences manifestReferences,
        AssemblyManifestDiagnostics diagnostics,
        AssemblyReferenceResolution references) =>
        diagnostics.UnresolvedReferences.SequenceEqual(
            references.References.Where(reference => !reference.Resolved).Select(reference => reference.Name),
            StringComparer.Ordinal)
        && manifestReferences.AssemblyIdentity is not null
        && IdentityEquals(manifestReferences.AssemblyIdentity, references.Identity)
        && ReferencesEqual(manifestReferences.References, references.References);

    private static IReadOnlyList<DecompiledDocument> ReadDocuments(
        string generationDirectory,
        AssemblyDecompilationManifest manifest)
    {
        var generatedFiles = manifest.Format.GeneratedFiles;
        if (generatedFiles.Count == 0) throw new InvalidDataException("Das Cachemanifest enthält keine Dokumente.");
        var normalizedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var documents = new List<DecompiledDocument>(generatedFiles.Count);
        foreach (var relativePath in generatedFiles)
        {
            var normalized = NormalizeSourcePath(relativePath);
            if (!normalizedFiles.Add(normalized)) throw new InvalidDataException("Das Cachemanifest enthält doppelte Dokumentpfade.");
            var fullPath = ResolveSafePath(generationDirectory, normalized);
            if (!File.Exists(fullPath) || !string.Equals(Path.GetExtension(fullPath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Das Cache-Dokument '{relativePath}' fehlt oder ist keine C#-Datei.");
            }

            var source = File.ReadAllText(fullPath, Utf8);
            if (string.IsNullOrWhiteSpace(source)) throw new InvalidDataException($"Das Cache-Dokument '{relativePath}' ist leer.");
            documents.Add(new DecompiledDocument(fullPath, Path.GetFileNameWithoutExtension(fullPath), source));
        }

        ValidateSourceFileSet(generationDirectory, normalizedFiles);
        return documents;
    }

    private static void ValidateSourceFileSet(string generationDirectory, HashSet<string> expected)
    {
        var sourceDirectory = ResolveSafePath(generationDirectory, AssemblyCacheContract.SourceDirectoryName);
        if (!Directory.Exists(sourceDirectory)) throw new InvalidDataException("Das Cacheverzeichnis 'source' fehlt.");
        var actual = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories)
            .Select(path => NormalizeSourcePath(Path.GetRelativePath(generationDirectory, path)))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!actual.SetEquals(expected)) throw new InvalidDataException("Manifest und vorhandene Cache-Dokumente stimmen nicht überein.");
    }

    private static IReadOnlyList<string> WriteDocuments(
        string generationDirectory,
        IReadOnlyList<DecompiledDocument> documents)
    {
        var paths = new List<string>(documents.Count);
        foreach (var (document, index) in documents.Select((value, index) => (value, index)))
        {
            if (string.IsNullOrWhiteSpace(document.CSharpSource)) throw new InvalidDataException("Eine dekompilierte Dokumenteinheit ist leer.");
            var relativePath = $"{AssemblyCacheContract.SourceDirectoryName}/{index:D5}-{SanitizeFileName(document.TypeMetadataName)}.cs";
            var fullPath = ResolveSafePath(generationDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            WriteTextAtomically(fullPath, document.CSharpSource);
            paths.Add(relativePath);
        }

        return paths;
    }

    private static AssemblyDecompilationManifest CreateManifest(
        AssemblyCachePublishRequest request,
        IReadOnlyList<string> generatedFiles) =>
        new()
        {
            Input = new AssemblyManifestInput
            {
                CacheKey = request.CacheKey.StableValue,
                CanonicalPath = request.Fingerprint.CanonicalPath,
                OriginalPath = request.Fingerprint.CanonicalPath,
                Length = request.Fingerprint.Length,
                MtimeUtc = request.Fingerprint.MtimeUtc,
                Sha256 = request.Fingerprint.Sha256,
            },
            References = new AssemblyManifestReferences
            {
                AssemblyIdentity = request.References.Identity,
                References = request.References.References,
            },
            Format = new AssemblyManifestFormat
            {
                DecompilerVersion = request.CacheKey.DecompilerVersion,
                OptionsIdentity = request.CacheKey.OptionsIdentity,
                CacheSchemaVersion = request.CacheKey.CacheSchemaVersion,
                GeneratedFiles = generatedFiles,
                Encoding = AssemblyCacheContract.Utf8EncodingName,
            },
            Diagnostics = new AssemblyManifestDiagnostics
            {
                Warnings = request.Decompilation.Diagnostics.Where(IsWarning).Select(diagnostic => diagnostic.Message).ToList(),
                Errors = request.Decompilation.Diagnostics.Where(diagnostic => !IsWarning(diagnostic)).Select(diagnostic => diagnostic.Message).ToList(),
                UnresolvedReferences = request.References.References.Where(reference => !reference.Resolved).Select(reference => reference.Name).ToList(),
            },
            Status = new AssemblyManifestStatus
            {
                CreatedUtc = DateTime.UtcNow,
                LastAccessUtc = DateTime.UtcNow,
                Status = request.Status.ToWireValue(),
                Complete = request.Status == AssemblySessionStatus.Complete,
            },
        };

    private static bool ReferencesEqual(
        IReadOnlyList<AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyReferenceDto> expected,
        IReadOnlyList<AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyReferenceDto> actual) =>
        expected.Count == actual.Count
        && expected.Zip(actual).All(pair =>
            string.Equals(pair.First.Name, pair.Second.Name, StringComparison.Ordinal)
            && string.Equals(pair.First.Version, pair.Second.Version, StringComparison.Ordinal)
            && string.Equals(pair.First.Culture, pair.Second.Culture, StringComparison.OrdinalIgnoreCase)
            && pair.First.Resolved == pair.Second.Resolved
            && string.Equals(pair.First.ResolvedPath, pair.Second.ResolvedPath, StringComparison.OrdinalIgnoreCase));

    private static bool IdentityEquals(
        AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyIdentityDto? expected,
        AiNetLinter.Mcp.Tools.AssemblyAnalysis.AssemblyIdentityDto? actual) =>
        expected is not null && actual is not null
        && string.Equals(expected.Name, actual.Name, StringComparison.Ordinal)
        && string.Equals(expected.Version, actual.Version, StringComparison.Ordinal)
        && string.Equals(expected.Culture, actual.Culture, StringComparison.OrdinalIgnoreCase)
        && string.Equals(expected.PublicKeyToken, actual.PublicKeyToken, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSourcePath(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(normalized)
            || normalized.StartsWith("../", StringComparison.Ordinal)
            || normalized.Contains("/../", StringComparison.Ordinal)
            || normalized.EndsWith("/..", StringComparison.Ordinal)
            || !normalized.StartsWith(AssemblyCacheContract.SourceDirectoryName + "/", StringComparison.OrdinalIgnoreCase)
            || !normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Das Manifest enthält einen unsicheren oder ungültigen Dokumentpfad.");
        }

        return normalized;
    }

    private static string ResolveSafePath(string root, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Der Cacheeintrag enthält einen unsicheren Dateipfad.");
        }

        return fullPath;
    }

    private static void WriteTextAtomically(string path, string value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, AssemblyCacheContract.FileBufferSize, FileOptions.WriteThrough);
        using var writer = new StreamWriter(stream, Utf8, leaveOpen: true);
        writer.Write(value);
        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    internal static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) || character is '.' or '+' or '`' ? '_' : character);
        }

        return builder.Length == 0 ? "assembly" : builder.ToString();
    }

    private static string HashSegment(string value, int length)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(value))).ToLowerInvariant();
        return hash[..Math.Min(length, hash.Length)];
    }

    private static bool IsWarning(AssemblySessionDiagnostic diagnostic) =>
        !string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase);

    private static bool IsCacheInputException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or InvalidDataException or ArgumentException or NotSupportedException;

    private static bool IsCacheWriteException(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException;

    private sealed record PointerPublishAttempt(bool Succeeded, AssemblySessionDiagnostic? Diagnostic);

}
