#nullable enable

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Baseline;

/// <summary>
/// Erkennt, ob ein SDK-Style-Projekt vor dem MSBuildWorkspace-Load per <c>dotnet restore</c>
/// aufbereitet wurde. MSBuildWorkspace laedt Projekte als Design-Time-Build (siehe
/// <see cref="AiNetLinter.Core.LinterEngine.CreateWorkspaceProperties"/>) und fuehrt dabei selbst
/// KEINEN Restore aus — anders als <c>dotnet build</c>, das implizit restored. Fehlt der Restore,
/// bleiben NuGet-Referenzen (PackageReference) im geladenen Project unaufloesbar; das sieht in der
/// Analyse wie tausende echte using-Phantome aus, obwohl <c>dotnet build</c> fuer dasselbe Projekt
/// fehlerfrei durchlaeuft. <c>obj/project.assets.json</c> ist der von jedem erfolgreichen
/// <c>dotnet restore</c>-Lauf erzeugte Marker — fehlt er oder ist er aelter als die .csproj, war
/// seit der letzten Projekt-Aenderung kein Restore mehr erfolgreich.
/// </summary>
internal static class ProjectRestoreState
{
    internal static bool NeedsRestore(string? projectFilePath)
    {
        if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath)) return false;

        var projectDir = Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrEmpty(projectDir)) return false;

        var assetsPath = Path.Combine(projectDir, "obj", "project.assets.json");
        if (!File.Exists(assetsPath)) return true;

        return File.GetLastWriteTimeUtc(projectFilePath) > File.GetLastWriteTimeUtc(assetsPath);
    }

    /// <summary>
    /// Liefert die IDs aller kompilierbaren Projekte der Solution, die nicht (mehr) restored sind.
    /// Nicht-Datei-Projekte (z. B. In-Memory-<c>AdhocWorkspace</c>-Solutions in Tests, <c>FilePath</c>
    /// == <see langword="null"/>) werden uebersprungen statt faelschlich als "braucht Restore"
    /// markiert zu werden.
    /// </summary>
    internal static IReadOnlySet<ProjectId> ComputeProjectsNeedingRestore(Solution solution)
    {
        var result = new HashSet<ProjectId>();
        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;
            if (NeedsRestore(project.FilePath)) result.Add(project.Id);
        }

        return result;
    }
}
