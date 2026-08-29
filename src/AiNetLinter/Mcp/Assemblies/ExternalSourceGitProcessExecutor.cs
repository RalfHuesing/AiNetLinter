#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
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
        bool wasTimedOut = false)
    {
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);
        ExitCode = exitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
        WasTimedOut = wasTimedOut;
    }

    internal int ExitCode { get; }

    internal string StandardOutput { get; }

    internal string StandardError { get; }

    internal bool WasTimedOut { get; }
}

internal interface IExternalSourceGitProcessExecutor
{
    Task<ExternalSourceGitProcessResult> ExecuteAsync(
        ExternalSourceGitProcessRequest request,
        CancellationToken cancellationToken = default);
}

internal sealed class ExternalSourceGitProcessExecutor : IExternalSourceGitProcessExecutor
{
    private static readonly TimeSpan ProcessTerminationTimeout = TimeSpan.FromSeconds(5);

    public async Task<ExternalSourceGitProcessResult> ExecuteAsync(
        ExternalSourceGitProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var process = new Process
        {
            StartInfo = CreateStartInfo(request),
        };
        if (!process.Start())
        {
            throw new InvalidOperationException("Der Git-Prozess konnte nicht gestartet werden.");
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeoutSource = new CancellationTokenSource(request.Timeout);
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linkedCancellation.Token).ConfigureAwait(false);
            var output = await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new ExternalSourceGitProcessResult(
                process.ExitCode,
                output[0],
                output[1]);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var aborted = await AbortProcessAsync(process).ConfigureAwait(false);
            if (!aborted)
            {
                throw new OperationCanceledException(
                    "Der Git-Prozess konnte nicht kontrolliert beendet werden.",
                    new InvalidOperationException("Die Prozessbeendigung wurde vom Betriebssystem abgelehnt."),
                    cancellationToken);
            }

            await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            var aborted = await AbortProcessAsync(process).ConfigureAwait(false);
            if (!aborted)
            {
                throw new TimeoutException("Der Git-Prozess konnte nicht kontrolliert beendet werden.");
            }

            var output = await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            return new ExternalSourceGitProcessResult(
                exitCode: -1,
                output[0],
                output[1],
                wasTimedOut: true);
        }
    }

    private static ProcessStartInfo CreateStartInfo(ExternalSourceGitProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
        };
        RemoveInheritedGitEnvironment(startInfo);
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var environmentVariable in request.Environment)
        {
            startInfo.Environment[environmentVariable.Key] = environmentVariable.Value;
        }

        return startInfo;
    }

    private static void RemoveInheritedGitEnvironment(ProcessStartInfo startInfo)
    {
        var inheritedGitVariables = new List<string>();
        foreach (var variableName in startInfo.Environment.Keys)
        {
            if (variableName.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
            {
                inheritedGitVariables.Add(variableName);
            }
        }

        foreach (var variableName in inheritedGitVariables)
        {
            startInfo.Environment.Remove(variableName);
        }
    }

    private static async Task<bool> AbortProcessAsync(Process process)
    {
        if (!TryKillProcessTree(process))
        {
            return false;
        }

        using var terminationTimeout = new CancellationTokenSource(ProcessTerminationTimeout);
        try
        {
            await process.WaitForExitAsync(terminationTimeout.Token).ConfigureAwait(false);
            return process.HasExited;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }

    private static bool TryKillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
            or Win32Exception
            or NotSupportedException)
        {
            return process.HasExited;
        }
    }
}
