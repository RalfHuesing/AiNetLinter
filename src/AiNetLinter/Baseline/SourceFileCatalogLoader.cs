#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace AiNetLinter.Baseline;

internal static class SourceFileCatalogLoader
{
    private static readonly object MsBuildRegistrationLock = new();

    internal static async Task<SourceFileCatalog> LoadAsync(string path, CancellationToken cancellationToken)
    {
        var solutionPath = FindSolutionFile(path);
        var workspace = CreateMSBuildWorkspace();
        var diagnostics = new ConcurrentBag<string>();
        workspace.RegisterWorkspaceFailedHandler(e => diagnostics.Add(e.Diagnostic.Message));

        try
        {
            var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);
            foreach (var message in diagnostics.Distinct(StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"[WARN]: Workspace-Diagnose: {message}");
            }

            return new SourceFileCatalog(workspace, solution, !diagnostics.IsEmpty);
        }
        catch
        {
            // Die Ownership des Workspace geht erst mit dem erfolgreichen Catalog-Aufbau an
            // SourceFileCatalog ueber. Bei Abbruch oder Fehler waere ein ungeordneter
            // MSBuildWorkspace-Dispose ein BuildHost-/Named-Pipe-Leak und kann beim Prozessende
            // den Roslyn-RpcServer mit "Pipe is broken" abstuerzen lassen.
            workspace.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Erzeugt den zentral registrierten Design-Time-MSBuild-Workspace.
    /// </summary>
    internal static MSBuildWorkspace CreateMSBuildWorkspace()
    {
        RegisterMSBuild();
        return MSBuildWorkspace.Create(LinterEngine.CreateWorkspaceProperties());
    }

    private static void RegisterMSBuild()
    {
        if (MSBuildLocator.IsRegistered) return;

        lock (MsBuildRegistrationLock)
        {
            if (MSBuildLocator.IsRegistered) return;

            BuildHostPatcher.PatchBuildHostForVs2026();
            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN]: Error during MSBuild registration: {ex.Message}");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILD_EXE_PATH", null);
                Environment.SetEnvironmentVariable("MSBuildExtensionsPath", null);
                Environment.SetEnvironmentVariable("MSBuildSDKsPath", null);
            }
        }
    }

    private static string FindSolutionFile(string path)
    {
        if (File.Exists(path)) return GetValidFile(path);
        if (Directory.Exists(path)) return SearchInDirectory(path);
        throw new FileNotFoundException($"Keine .sln oder .slnx Datei gefunden unter: {path}");
    }

    private static string GetValidFile(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".sln" or ".slnx") return path;
        throw new FileNotFoundException($"Keine gültige Solution-Datei: {path}");
    }

    private static string SearchInDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.slnx")
            .Concat(Directory.GetFiles(directory, "*.sln"))
            .ToArray();
        if (files.Length > 0) return files[0];
        throw new FileNotFoundException($"Keine .sln oder .slnx Datei im Verzeichnis gefunden: {directory}");
    }
}
