#nullable enable

using System;
using System.Threading;

namespace AiNetLinter.Baseline;

internal sealed record FileSystemWalkOptions
{
    private FileSystemWalkOptions(
        int maxDepth,
        bool skipExcludedDirectories,
        CancellationToken cancellationToken)
    {
        MaxDepth = maxDepth;
        SkipExcludedDirectories = skipExcludedDirectories;
        CancellationToken = cancellationToken;
    }

    internal int MaxDepth { get; }

    internal bool SkipExcludedDirectories { get; }

    internal CancellationToken CancellationToken { get; }

    internal static FileSystemWalkOptions Default(CancellationToken cancellationToken) =>
        new(int.MaxValue, skipExcludedDirectories: true, cancellationToken);

    internal static FileSystemWalkOptions ForFileTree(
        int? maxDepth,
        CancellationToken cancellationToken)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "Die maximale Tiefe darf nicht negativ sein.");
        }

        return new FileSystemWalkOptions(maxDepth ?? int.MaxValue, skipExcludedDirectories: true, cancellationToken);
    }
}
