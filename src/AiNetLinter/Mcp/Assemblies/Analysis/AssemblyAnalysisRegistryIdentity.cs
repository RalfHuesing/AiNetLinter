#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies.ExternalSource.Snapshots;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

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

    internal static async Task<string?> ResolveCurrentSourceSnapshotIdentityAsync(
        IAssemblySourceResolver? sourceOrchestrator,
        string canonicalPath,
        CancellationToken cancellationToken)
    {
        if (sourceOrchestrator is null) return null;

        AssemblySourceResolution resolution;
        try
        {
            resolution = await sourceOrchestrator.ResolveForRegistryAsync(
                    canonicalPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Ein nicht ermittelbarer aktueller Source-Stand darf keinen
            // veralteten source-backed Eintrag als frisch erscheinen lassen.
            return null;
        }

        try
        {
            return resolution.Selection?.SourceLease.Snapshot.Identity.StableValue;
        }
        finally
        {
            AssemblyAnalysisRegistryDisposal.TryDispose(
                resolution.Lifetime,
                "Source-Selection-Scope nach Freshness-Probe");
        }
    }
}
