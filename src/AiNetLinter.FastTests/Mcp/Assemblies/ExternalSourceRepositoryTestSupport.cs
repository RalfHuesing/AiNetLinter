#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

/// <summary>
/// Sammelt Log-Ereignisse eines einzelnen Test-Loggers ohne den globalen
/// Serilog-Logger zu verändern.
/// </summary>
internal sealed class ExternalSourceRepositoryTestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> events = new();

    internal LogEvent[] Events => events.ToArray();

    public void Emit(LogEvent logEvent)
    {
        events.Enqueue(logEvent);
    }
}

/// <summary>
/// Prüft vor dem echten Reparse-Test die lokale Fähigkeit für Directory-Symlinks.
/// Ein übersprungener Preflight ist kein Sicherheitsnachweis.
/// </summary>
internal static class WindowsReparseCapabilityGate
{
    internal static void Require()
    {
        using var preflight = TestTempDirectory.Create("external-source-reparse-capability-");
        var targetPath = preflight.CreateSubdirectory("target");
        var linkPath = preflight.GetPath("link");
        var linkCreated = false;
        try
        {
            try
            {
                Directory.CreateSymbolicLink(linkPath, targetPath);
                linkCreated = true;
            }
            catch (Exception exception) when (
                ExternalSourceRepositoryFailurePolicy.IsPrivilegeNotHeld(exception))
            {
                Assert.Skip(
                    "Der Testhost meldet ERROR_PRIVILEGE_NOT_HELD (1314) für "
                    + "Directory.CreateSymbolicLink. Die Symlink-Capability wurde "
                    + "nicht nachgewiesen; dieser Skip ist kein Sicherheitsnachweis. "
                    + "Der echte Reparse-Test muss privilegiert ohne Skip wiederholt werden.");
            }

            var attributes = File.GetAttributes(linkPath);
            Assert.True(
                attributes.HasFlag(FileAttributes.ReparsePoint),
                "Der Capability-Preflight hat keinen echten Directory-Reparse-Punkt erzeugt.");
        }
        finally
        {
            if (linkCreated)
            {
                Directory.Delete(linkPath);
            }
        }
    }

}

internal static class ExternalSourceRepositoryFixtureOperations
{
    internal static void CopyBaselineMiniSolution(string sourceRoot, string destination)
    {
        Directory.CreateDirectory(destination);
        File.Copy(
            Path.Combine(sourceRoot, "BaselineMini.slnx"),
            Path.Combine(destination, "BaselineMini.slnx"));
    }
}
