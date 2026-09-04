#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.Analysis;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Coordinators;

internal static class AssemblyAnalysisRegistryIdentity
{
    internal static bool TryCreateFingerprint(
        string canonicalPath,
        Func<string, AssemblyFingerprint>? fingerprintFactory,
        out AssemblyFingerprint? fingerprint,
        out AssemblySessionDiagnostic? diagnostic)
    {
        if (fingerprintFactory is null)
        {
            return AssemblyFingerprintCalculator.TryCreate(canonicalPath, out fingerprint, out diagnostic);
        }

        try
        {
            fingerprint = fingerprintFactory(canonicalPath);
            diagnostic = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            fingerprint = null;
            diagnostic = new(
                AssemblyDiagnosticCodes.For(nameof(AssemblyFingerprintCalculator), nameof(AssemblyFingerprintCalculator.TryCreate)),
                $"Assembly-Fingerprint konnte nicht berechnet werden: {exception.Message}",
                AssemblyDiagnosticSeverity.Error);
            return false;
        }
    }
}
