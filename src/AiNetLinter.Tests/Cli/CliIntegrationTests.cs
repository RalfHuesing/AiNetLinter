using Xunit;
using System.Diagnostics;
using System.IO;

namespace AiNetLinter.Tests.Cli;

public sealed class CliIntegrationTests
{
    [Fact]
    public void RunLinterCli_OnMainProject_ReturnsSuccess()
    {
        // Arrange
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "src", "AiNetLinter.Tests", "Cli", "rules.json");
        var targetPath = Path.Combine(rootDir, "src", "AiNetLinter");

        Assert.True(File.Exists(linterDllPath), $"Linter-DLL nicht gefunden unter: {linterDllPath}");
        Assert.True(File.Exists(configPath), $"Konfigurationsdatei nicht gefunden unter: {configPath}");
        Assert.True(Directory.Exists(targetPath), $"Zielverzeichnis nicht gefunden unter: {targetPath}");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Assert
        Assert.Null(error == "" ? null : error);
        Assert.True(process.ExitCode == 0, $"Linter schlug mit Exit-Code {process.ExitCode} fehl. Output:\n{output}\nError:\n{error}");
        Assert.Contains("[SUCCESS]", output);
    }

    [Fact]
    public void RunLinterCli_WithInvalidConfig_ReturnsErrorExitCode()
    {
        // Arrange
        var rootDir = FindSolutionRoot();
        var linterDllPath = FindLinterDll(rootDir);
        var configPath = Path.Combine(rootDir, "src", "AiNetLinter.Tests", "Cli", "non-existent-config.json");
        var targetPath = Path.Combine(rootDir, "src", "AiNetLinter");

        var processInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{linterDllPath}\" --config \"{configPath}\" --path \"{targetPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Act
        using var process = Process.Start(processInfo);
        Assert.NotNull(process);

        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        // Assert
        Assert.True(process.ExitCode == 1, $"Linter sollte mit Exit-Code 1 fehlschlagen, beendete aber mit {process.ExitCode}.");
        Assert.Contains("[ERROR]", error);
    }

    private static string FindSolutionRoot()
    {
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (currentDir != null)
        {
            if (File.Exists(Path.Combine(currentDir.FullName, "AiNetLinter.slnx")))
            {
                return currentDir.FullName;
            }
            currentDir = currentDir.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit der Projektmappe 'AiNetLinter.slnx' wurde nicht gefunden.");
    }

    private static string FindLinterDll(string rootDir)
    {
        var binDir = Path.Combine(rootDir, "src", "AiNetLinter", "bin");
        if (!Directory.Exists(binDir))
        {
            throw new DirectoryNotFoundException($"Das Build-Ausgabeverzeichnis existiert nicht: {binDir}");
        }

        // Suche nach AiNetLinter.dll in Debug/Release Ordnern
        var files = Directory.GetFiles(binDir, "AiNetLinter.dll", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            throw new FileNotFoundException("Die Datei 'AiNetLinter.dll' wurde in keinem Build-Unterordner gefunden.");
        }

        // Falls mehrere existieren (z. B. Debug und Release), nimm den aktuellsten
        return files[0];
    }
}
