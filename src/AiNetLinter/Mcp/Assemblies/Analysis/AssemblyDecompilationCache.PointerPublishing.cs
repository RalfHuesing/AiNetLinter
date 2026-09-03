#nullable enable

using System;
using System.IO;
using System.Text.Json;
using AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed partial class AssemblyDecompilationCache
{
    private PointerPublishOutcome TryPublishPointer(
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
            if (TryRead(readRequest, out _, out _)) return PointerPublishOutcome.Existing;
            var attemptResult = PublishPointerAttempt(pointerPath, generationName, readRequest);
            if (attemptResult.Succeeded)
            {
                return attemptResult.GenerationPublished
                    ? PointerPublishOutcome.Published
                    : PointerPublishOutcome.Existing;
            }

            diagnostic = attemptResult.Diagnostic;
        }

        diagnostic ??= new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCachePublishRequest)), "Current-Pointer konnte nach begrenzten Versuchen nicht validiert veröffentlicht werden.", AssemblyDiagnosticSeverity.Error);
        return PointerPublishOutcome.Failed;
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
            beforePointerValidation?.Invoke(
                Path.Combine(Path.GetDirectoryName(pointerPath)!, generationName));
            var succeeded = TryRead(readRequest, out _, out _);
            var generationPublished = succeeded
                && string.Equals(
                    Path.GetFileName(ReadPointer(Path.GetDirectoryName(pointerPath)!, pointerPath)),
                    generationName,
                    StringComparison.OrdinalIgnoreCase);
            return new(
                succeeded,
                generationPublished,
                succeeded ? null : new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheContract.CurrentPointerFileName)), "Der neu veröffentlichte Current-Pointer konnte nicht erneut validiert werden.", AssemblyDiagnosticSeverity.Warning));
        }
        catch (IOException ex)
        {
            var diagnostic = new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationCache), nameof(AssemblyCacheContract.CurrentPointerFileName)), $"Current-Pointer konnte nicht ersetzt werden: {ex.Message}", AssemblyDiagnosticSeverity.Warning);
            var succeeded = TryRead(readRequest, out _, out _);
            var generationPublished = succeeded
                && string.Equals(
                    Path.GetFileName(ReadPointer(Path.GetDirectoryName(pointerPath)!, pointerPath)),
                    generationName,
                    StringComparison.OrdinalIgnoreCase);
            return new(succeeded, generationPublished, diagnostic);
        }
        finally
        {
            AssemblyCacheCleanup.DeleteFile(temporaryPointer);
        }
    }

    private static bool IsGenerationReferencedByPointer(
        string entryDirectory,
        string generationDirectory)
    {
        var pointerPath = Path.Combine(entryDirectory, AssemblyCacheContract.CurrentPointerFileName);
        try
        {
            var currentGeneration = ReadPointer(entryDirectory, pointerPath);
            return string.Equals(
                currentGeneration,
                Path.GetFullPath(generationDirectory),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            return true;
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

        var generationDirectory = AssemblyCacheGenerationStorage.ResolveSafePath(entryDirectory, normalized);
        if (!Directory.Exists(generationDirectory)) throw new InvalidDataException("Die referenzierte Cachegeneration fehlt.");
        return generationDirectory;
    }

    private enum PointerPublishOutcome
    {
        Failed,
        Existing,
        Published,
    }

    private sealed record PointerPublishAttempt(
        bool Succeeded,
        bool GenerationPublished,
        AssemblySessionDiagnostic? Diagnostic);
}
