#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed record SourceSnapshotIdentity
{
    private const string StableValueSeparator = "|";
    private const string StableValueLengthSeparator = ":";

    private SourceSnapshotIdentity(
        string repositoryUrl,
        string loadedRevision,
        string solutionPath)
    {
        RepositoryUrl = repositoryUrl;
        LoadedRevision = loadedRevision;
        SolutionPath = solutionPath;
        StableValue = BuildStableValue(repositoryUrl, loadedRevision, solutionPath);
    }

    internal string RepositoryUrl { get; }

    internal string LoadedRevision { get; }

    internal string SolutionPath { get; }

    internal string StableValue { get; }

    internal static SourceSnapshotIdentity Create(
        ExternalSourceMapping mapping,
        string loadedRevision)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (string.IsNullOrWhiteSpace(loadedRevision))
        {
            throw new ArgumentException(
                "Die tatsächlich geladene Revision darf nicht leer sein.",
                nameof(loadedRevision));
        }

        return new(
            CanonicalizeRepositoryUrl(mapping.Url),
            loadedRevision.Trim(),
            CanonicalizeSolutionPath(mapping.SolutionPath));
    }

    private static string CanonicalizeRepositoryUrl(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || uri is null
            || uri.Host.Length == 0
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException(
                "Die Repository-URL muss eine absolute HTTP(S)-URL sein.",
                nameof(value));
        }

        return uri.AbsoluteUri;
    }

    private static string CanonicalizeSolutionPath(string value)
    {
        var path = value.Trim().Replace('\\', '/');
        EnsureRelativeSolutionPath(path, value);
        var segments = NormalizeSolutionSegments(path, value);

        var normalized = string.Join('/', segments);
        EnsureSolutionExtension(normalized, value);

        return normalized;
    }

    private static void EnsureRelativeSolutionPath(string path, string originalValue)
    {
        if (path.Length == 0
            || Path.IsPathRooted(path)
            || path.StartsWith("/", StringComparison.Ordinal)
            || IsDriveQualified(path))
        {
            throw new ArgumentException(
                "Der Solution-Pfad muss repository-relativ sein.",
                nameof(originalValue));
        }
    }

    private static List<string> NormalizeSolutionSegments(string path, string originalValue)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is ".")
            {
                continue;
            }

            if (segment is "..")
            {
                if (segments.Count == 0)
                {
                    throw new ArgumentException(
                        "Der Solution-Pfad darf nicht aus dem Repository ausbrechen.",
                        nameof(originalValue));
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Der Solution-Pfad enthält ein ungültiges Segment.",
                    nameof(originalValue));
            }

            segments.Add(segment);
        }

        return segments;
    }

    private static void EnsureSolutionExtension(string path, string originalValue)
    {
        if (path.Length == 0
            || !(path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Der Solution-Pfad muss auf .sln oder .slnx enden.",
                nameof(originalValue));
        }
    }

    private static string BuildStableValue(params string[] components)
    {
        var parts = new string[components.Length];
        for (var index = 0; index < components.Length; index++)
        {
            parts[index] = string.Concat(
                components[index].Length.ToString(CultureInfo.InvariantCulture),
                StableValueLengthSeparator,
                components[index]);
        }

        return string.Join(StableValueSeparator, parts);
    }

    private static bool IsDriveQualified(string value) =>
        value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
}

internal sealed class ExternalSourceSnapshot : IDisposable
{
    private readonly Workspace workspace;
    private int disposed;

    internal ExternalSourceSnapshot(
        SourceSnapshotIdentity identity,
        Solution solution,
        Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(solution);
        ArgumentNullException.ThrowIfNull(workspace);

        Identity = identity;
        Solution = solution;
        this.workspace = workspace;
    }

    internal SourceSnapshotIdentity Identity { get; }

    internal Solution Solution { get; }

    internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        workspace.Dispose();
    }
}
