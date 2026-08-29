#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceGitProcessRequest
{
    internal ExternalSourceGitProcessRequest(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string> environment)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Der Prozessname darf nicht leer sein.", nameof(fileName));
        }

        ArgumentNullException.ThrowIfNull(arguments);
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException(
                "Das Prozess-Arbeitsverzeichnis darf nicht leer sein.",
                nameof(workingDirectory));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        ArgumentNullException.ThrowIfNull(environment);
        FileName = fileName;
        Arguments = arguments.ToImmutableArray();
        WorkingDirectory = workingDirectory;
        Timeout = timeout;
        Environment = environment.ToImmutableDictionary(StringComparer.Ordinal);
    }

    internal string FileName { get; }

    internal ImmutableArray<string> Arguments { get; }

    internal string WorkingDirectory { get; }

    internal TimeSpan Timeout { get; }

    internal ImmutableDictionary<string, string> Environment { get; }
}

internal sealed class ExternalSourceGitProcessResult
{
    internal ExternalSourceGitProcessResult(
        int exitCode,
        string standardOutput,
        string standardError,
        ExternalSourceGitProcessResultOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        options ??= new ExternalSourceGitProcessResultOptions();
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        WasTimedOut = options.WasTimedOut;
        StandardOutputTruncated = options.StandardOutputTruncated;
        StandardErrorTruncated = options.StandardErrorTruncated;
    }

    internal int ExitCode { get; }

    internal string StandardOutput { get; }

    internal string StandardError { get; }

    internal bool WasTimedOut { get; }

    internal bool StandardOutputTruncated { get; }

    internal bool StandardErrorTruncated { get; }
}

internal sealed class ExternalSourceGitProcessResultOptions
{
    internal bool WasTimedOut { get; init; }

    internal bool StandardOutputTruncated { get; init; }

    internal bool StandardErrorTruncated { get; init; }
}

internal interface IExternalSourceGitProcessExecutor
{
    Task<ExternalSourceGitProcessResult> ExecuteAsync(
        ExternalSourceGitProcessRequest request,
        CancellationToken cancellationToken = default);
}
