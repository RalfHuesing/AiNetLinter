#nullable enable

using System;
using System.IO;
using System.Text;
using AiNetLinter.Tests.Fixtures;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Generiert synthetische Loesungen in einem <see cref="TestTempDirectory"/>, um Performance-
/// und Skalierungs-Verhalten des MCP-Servers reproduzierbar zu messen. Pro Projekt wird ein
/// kompakter <c>.csproj</c>-Stub plus <c>N</c> Quelldateien mit konfigurierbarer
/// Zeilenanzahl geschrieben; eine <c>.slnx</c> listet alle Projekte. Kompilation ist nicht
/// erforderlich — die Engine laedt die Loesung via <c>MSBuildWorkspace.OpenSolutionAsync</c>,
/// die generierten Quellen werden on-demand ueber <c>FileTextLoader</c> gelesen.
/// </summary>
public static class LoadFixtureBuilder
{
    /// <summary>
    /// Erstellt eine Synthetic-Solution in einem neuen <see cref="TestTempDirectory"/>.
    /// </summary>
    /// <param name="name">Anzeige-Name (z. B. "1k-loc"). Geht in den Prefix des Temp-Verzeichnisses ein.</param>
    /// <param name="projectCount">Anzahl der zu erstellenden <c>.csproj</c>-Projekte.</param>
    /// <param name="filesPerProject">Anzahl der <c>.cs</c>-Dateien pro Projekt.</param>
    /// <param name="linesPerFile">Zeilen pro Datei (mind. 3 fuer die Huelle).</param>
    public static LoadFixtureHandle Build(
        string name,
        int projectCount,
        int filesPerProject,
        int linesPerFile)
    {
        if (projectCount < 1) throw new ArgumentOutOfRangeException(nameof(projectCount));
        if (filesPerProject < 1) throw new ArgumentOutOfRangeException(nameof(filesPerProject));
        if (linesPerFile < 3) throw new ArgumentOutOfRangeException(nameof(linesPerFile));

        var tempDir = TestTempDirectory.Create($"ainetlinter-load-{SanitizeName(name)}-");
        var solutionDir = tempDir.DirectoryPath;

        for (var p = 0; p < projectCount; p++)
        {
            var projectName = $"Project{p:D3}";
            var projectDir = Path.Combine(solutionDir, "src", projectName);
            Directory.CreateDirectory(projectDir);

            File.WriteAllText(
                Path.Combine(projectDir, $"{projectName}.csproj"),
                BuildCsProj(projectName));

            for (var f = 0; f < filesPerProject; f++)
            {
                var fileName = $"File{f:D4}.cs";
                File.WriteAllText(
                    Path.Combine(projectDir, fileName),
                    BuildSourceFile(projectName, f, linesPerFile));
            }
        }

        var solutionPath = Path.Combine(solutionDir, "Synthetic.slnx");
        File.WriteAllText(solutionPath, BuildSlnx(projectCount));

        return new LoadFixtureHandle(name, tempDir, solutionPath);
    }

    private static string SanitizeName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        }
        return sb.ToString();
    }

    private static string BuildCsProj(string projectName)
    {
        return
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n" +
            "  <PropertyGroup>\n" +
            "    <TargetFramework>net10.0</TargetFramework>\n" +
            "    <ImplicitUsings>enable</ImplicitUsings>\n" +
            "    <Nullable>enable</Nullable>\n" +
            "  </PropertyGroup>\n" +
            "</Project>\n";
    }

    private static string BuildSlnx(int projectCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Solution>");
        sb.AppendLine("  <Folder Name=\"/src/\">");
        for (var p = 0; p < projectCount; p++)
        {
            var projectName = $"Project{p:D3}";
            sb.AppendLine($"    <Project Path=\"src/{projectName}/{projectName}.csproj\" />");
        }
        sb.AppendLine("  </Folder>");
        sb.AppendLine("</Solution>");
        return sb.ToString();
    }

    private static string BuildSourceFile(string projectName, int fileIndex, int linesPerFile)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {projectName};");
        sb.AppendLine();
        sb.AppendLine($"public class Class{fileIndex:D4}");
        sb.AppendLine("{");
        // linesPerFile = 3 (Huelle) + N (Body-Methoden)
        var bodyLines = linesPerFile - 3;
        for (var i = 0; i < bodyLines; i++)
        {
            sb.AppendLine($"    public int Method{i:D4}() => {i};");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }
}
