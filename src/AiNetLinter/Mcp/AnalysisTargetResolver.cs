#nullable enable

using System;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static class AnalysisTargetResolver
{
    internal static AnalysisTargetResolution Resolve(AnalysisTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TargetType))
        {
            return Invalid("Der Parameter 'targetType' ist erforderlich.");
        }

        var targetType = ResolveTargetType(request.TargetType);
        if (targetType is null)
        {
            return Invalid("Der Parameter 'targetType' muss exakt 'project' oder 'assembly' sein.");
        }

        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return Invalid("Der Parameter 'targetPath' ist erforderlich.");
        }

        var path = ResolveCanonicalPath(request.TargetPath);
        if (path.Error is not null)
        {
            return Invalid(path.Error);
        }

        if (targetType == AnalysisTargetType.Assembly && !File.Exists(path.CanonicalPath!))
        {
            return Invalid(
                $"Der Assembly-Pfad muss auf eine vorhandene Datei zeigen: '{path.CanonicalPath}'.",
                "targetPath muss ein existierender absoluter lokaler .dll- oder .exe-Pfad sein.");
        }

        if (targetType == AnalysisTargetType.Assembly
            && !AssemblyPathValidation.IsSupportedAssemblyPath(path.CanonicalPath!))
        {
            return Invalid(
                $"Der Assembly-Pfad muss auf eine .dll- oder .exe-Datei zeigen: '{path.CanonicalPath}'.",
                "targetPath muss auf eine vorhandene .dll- oder .exe-Datei zeigen.");
        }

        return new(new AnalysisTarget(targetType.Value, path.CanonicalPath!, request), null);
    }

    internal static AnalysisTargetResolution ResolveOptional(AnalysisTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetType is null && request.TargetPath is null)
        {
            return new(null, null);
        }

        return Resolve(request);
    }

    private static AnalysisTargetType? ResolveTargetType(string targetType) =>
        targetType.Trim().ToLowerInvariant() switch
        {
            "project" => AnalysisTargetType.Project,
            "assembly" => AnalysisTargetType.Assembly,
            _ => null,
        };

    private static PathResolution ResolveCanonicalPath(string targetPath)
    {
        var path = targetPath.Trim();
        if (!Path.IsPathFullyQualified(path))
        {
            return new(null, "Der Parameter 'targetPath' muss ein absoluter Pfad sein.");
        }

        try
        {
            var canonicalPath = Path.GetFullPath(path);
            return new(canonicalPath, null);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return new(null, $"Der Parameter 'targetPath' ist kein gültiger Pfad: '{targetPath}'.");
        }
    }

    private static AnalysisTargetResolution Invalid(string message, string? hint = null) =>
        new(null, McpToolResults.InvalidArgument(
            message,
            hint ?? "targetType ('project' oder 'assembly') und targetPath (absoluter Pfad) gemäß Spezifikation übergeben."));

    private sealed record PathResolution(string? CanonicalPath, string? Error);
}
