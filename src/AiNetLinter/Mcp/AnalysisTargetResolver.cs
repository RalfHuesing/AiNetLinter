#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AiNetLinter.Configuration;
using AiNetLinter.Output;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp;

internal static class AnalysisTargetResolver
{
    internal static AnalysisTargetResolution Resolve(AnalysisTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ResolveTargetPathOnly(request);
    }

    /// <summary>
    /// Loest den releaseweiten Kernvertrag auf: genau ein absoluter, vorhandener
    /// Dateipfad. Die Dateiendung bestimmt Source oder Decompiled-Assembly.
    /// </summary>
    internal static AnalysisTargetResolution ResolveTargetPathOnly(AnalysisTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TargetPath))
        {
            return Invalid(
                "Der Parameter 'targetPath' ist erforderlich.",
                "Den absoluten Pfad einer vorhandenen .sln, .slnx, .dll oder .exe uebergeben.");
        }

        var path = ResolveCanonicalFilePath(request.TargetPath);
        if (path.Error is not null)
        {
            return Invalid(path.Error, path.Hint);
        }

        var canonicalPath = path.CanonicalPath!;
        var targetKind = ResolveTargetTypeFromExtension(canonicalPath);

        if (targetKind is null)
        {
            return Invalid(
                $"Der Parameter 'targetPath' hat eine nicht unterstuetzte Endung: '{canonicalPath}'.",
                "Eine vorhandene Datei mit Endung .sln, .slnx, .dll oder .exe uebergeben.");
        }

        var analysisRoot = Path.GetDirectoryName(canonicalPath)!;
        var isSource = targetKind == AnalysisTargetType.Project;
        var rulesPath = isSource
            ? Path.Combine(analysisRoot, ConfigLoader.FileName)
            : null;
        var fingerprint = CreateFingerprint(canonicalPath);
        var target = new AnalysisTarget(targetKind.Value, canonicalPath, request)
        {
            AnalysisRoot = analysisRoot,
            Fingerprint = fingerprint,
            RulesPath = rulesPath,
            Capabilities = new(
                AnalysisCapabilityStatus.Supported,
                isSource && File.Exists(rulesPath)
                    ? AnalysisCapabilityStatus.Supported
                    : isSource
                        ? AnalysisCapabilityStatus.NotConfigured
                        : AnalysisCapabilityStatus.Unsupported),
        };
        return new(target, null);
    }

    internal static AnalysisTarget ResolveRequiredSourceTarget(string? targetPath)
    {
        var resolution = ResolveTargetPathOnly(new AnalysisTargetRequest(targetPath));
        if (resolution.Target is { TargetType: AnalysisTargetType.Project } target)
        {
            return target;
        }

        var message = resolution.Error?.Content
            .OfType<TextContentBlock>()
            .Select(block => block.Text)
            .FirstOrDefault()
            ?? "Resources akzeptieren nur den absoluten Pfad einer vorhandenen .sln- oder .slnx-Datei.";
        throw new McpException(message);
    }

    private static AnalysisTargetType? ResolveTargetTypeFromExtension(string canonicalPath) =>
        Path.GetExtension(canonicalPath).ToLowerInvariant() switch
        {
            ".sln" or ".slnx" => AnalysisTargetType.Project,
            ".dll" or ".exe" => AnalysisTargetType.Assembly,
            _ => null,
        };

    internal static AnalysisTargetResolution ResolveOptional(AnalysisTargetRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TargetPath is null)
        {
            return new(null, null);
        }

        return Resolve(request);
    }

    private static PathResolution ResolveCanonicalFilePath(string targetPath)
    {
        var path = targetPath.Trim();
        if (!Path.IsPathFullyQualified(path))
        {
            return new(null, "Der Parameter 'targetPath' muss ein absoluter Pfad sein.",
                "targetPath mit einem absoluten Dateipfad angeben.");
        }

        string canonicalPath;
        try
        {
            canonicalPath = Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return new(null, $"Der Parameter 'targetPath' ist kein gueltiger Pfad: '{targetPath}'.",
                "Einen gueltigen absoluten Dateipfad angeben.");
        }

        if (Directory.Exists(canonicalPath))
        {
            return new(null,
                $"Der Parameter 'targetPath' muss auf eine Datei zeigen, kein Verzeichnis: '{canonicalPath}'.",
                "Eine konkrete vorhandene .sln/.slnx/.dll/.exe-Datei angeben.");
        }

        if (!File.Exists(canonicalPath))
        {
            return new(null,
                $"Der Parameter 'targetPath' muss auf eine vorhandene Datei zeigen: '{canonicalPath}'.",
                "Eine vorhandene .sln/.slnx/.dll/.exe-Datei angeben.");
        }

        return new(canonicalPath, null, null);
    }

    private static string CreateFingerprint(string canonicalPath)
    {
        using var stream = File.OpenRead(canonicalPath);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static AnalysisTargetResolution Invalid(string message, string? hint = null) =>
        new(null, McpToolResults.InvalidArgument(
            message,
            hint ?? "targetPath mit dem absoluten Pfad einer vorhandenen .sln/.slnx/.dll/.exe-Datei übergeben.",
            fieldPath: "$.targetPath"));

    private sealed record PathResolution(string? CanonicalPath, string? Error, string? Hint = null);
}
