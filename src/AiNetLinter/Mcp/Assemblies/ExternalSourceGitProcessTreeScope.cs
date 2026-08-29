#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class ExternalSourceGitProcessTreeScope : IDisposable
{
    private readonly ExternalSourceGitProcessNativeJob job;
    private readonly ExternalSourceGitProcessNativeOperations operations;
    private readonly StreamReader standardOutput;
    private readonly StreamReader standardError;
    private int disposed;

    private ExternalSourceGitProcessTreeScope(
        ExternalSourceGitProcessLaunch launch,
        ExternalSourceGitProcessNativeOperations operations)
    {
        Process = launch.Process;
        job = launch.Job;
        this.operations = operations;
        standardOutput = launch.StandardOutput;
        standardError = launch.StandardError;
    }

    internal Process Process { get; }

    internal StreamReader StandardOutput => standardOutput;

    internal StreamReader StandardError => standardError;

    internal static ExternalSourceGitProcessTreeScope Start(
        ProcessStartInfo startInfo,
        ExternalSourceGitProcessNativeOperations operations) =>
        new(ExternalSourceGitProcessLauncher.Start(startInfo, operations), operations);

    internal bool TryTerminate(ICollection<Exception> failures) =>
        ExternalSourceGitProcessLauncher.TryTerminate(job, operations, failures);

    internal void CloseOutputStreams(ICollection<Exception> failures)
    {
        CloseStream(standardOutput, failures);
        CloseStream(standardError, failures);
    }

    internal void Dispose(ICollection<Exception> failures)
    {
        if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        CloseOutputStreams(failures);
        job.Close(failures);
        Process.Dispose();
    }

    public void Dispose()
    {
        var failures = new List<Exception>();
        Dispose(failures);
        if (failures.Count > 0)
        {
            throw new AggregateException(
                "Der Git-Prozessbaum konnte nicht vollständig freigegeben werden.",
                failures);
        }
    }

    private static void CloseStream(StreamReader stream, ICollection<Exception> failures)
    {
        try
        {
            stream.Dispose();
        }
        catch (Exception exception) when (IsExpectedProcessException(exception))
        {
            failures.Add(exception);
        }
    }

    private static bool IsExpectedProcessException(Exception exception) =>
        exception is InvalidOperationException
            or ObjectDisposedException
            or IOException;
}
