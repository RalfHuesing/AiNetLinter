#nullable enable

using System;
using System.IO;
using System.Security.Cryptography;

namespace AiNetLinter.Mcp.Assemblies;

internal static class AssemblyFingerprintCalculator
{
    internal static AssemblyFingerprint Create(string assemblyPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        var canonicalPath = Canonicalize(assemblyPath);
        var fileInfo = new FileInfo(canonicalPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("Die Assembly-Datei wurde nicht gefunden.", canonicalPath);
        }

        using var stream = new FileStream(canonicalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var hash = SHA256.HashData(stream);
        return new AssemblyFingerprint(
            canonicalPath,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc,
            Convert.ToHexString(hash));
    }

    internal static bool TryCreate(string? assemblyPath, out AssemblyFingerprint? fingerprint, out AssemblySessionDiagnostic? diagnostic)
    {
        fingerprint = null;
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            diagnostic = new(AssemblyDiagnosticCodes.For(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.Canonicalize)), "Der Assembly-Pfad fehlt oder ist leer.", "error");
            return false;
        }

        try
        {
            fingerprint = Create(assemblyPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostic = new(AssemblyDiagnosticCodes.For(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.TryCreate)), $"Assembly-Fingerprint konnte nicht berechnet werden: {ex.Message}", "error");
            return false;
        }
    }

    internal static AssemblyDecompilationCacheKey CreateCacheKey(
        AssemblyFingerprint fingerprint,
        AssemblyDecompilationOptions options) =>
        new(
            fingerprint.CanonicalPath,
            fingerprint.Sha256,
            options.DecompilerVersion,
            options.Identity,
            options.CacheSchemaVersion);

    internal static string Canonicalize(string assemblyPath)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        return Path.TrimEndingDirectorySeparator(fullPath);
    }
}
