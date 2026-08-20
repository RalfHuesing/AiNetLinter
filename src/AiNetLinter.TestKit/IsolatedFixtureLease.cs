#nullable enable

using System;
using System.IO;
using System.Linq;

namespace AiNetLinter.TestKit;

/// <summary>
/// Isolierte Temp-Kopie einer kanonischen Mini-Solution aus <c>tests/Fixtures/&lt;fixtureFolderName&gt;/</c>.
/// Reine Datei-I/O ohne MSBuild-/xUnit-Abhaengigkeit, damit dieser Baustein
/// sowohl von <see cref="AiNetLinter.TestKit"/>-Konsumenten selbst als auch von einem echten
/// MSBuild-Ladepfad (Integration-Ebene) verwendbar ist, ohne die Deny-Liste von
/// <c>FastTestsDependencyGuardTests</c> zu beruehren.
/// </summary>
public sealed class IsolatedFixtureLease : IDisposable
{
    private readonly TestTempDirectory tempDir;

    private IsolatedFixtureLease(TestTempDirectory tempDir)
    {
        this.tempDir = tempDir;
        RootPath = tempDir.DirectoryPath;
    }

    /// <summary>
    /// Wurzelverzeichnis der isolierten Kopie.
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    /// Kopiert <c>tests/Fixtures/&lt;fixtureFolderName&gt;/</c> unterhalb von <paramref name="solutionRoot"/>
    /// unter Auslassung von <c>bin</c>/<c>obj</c>-Unterordnern in ein neues, eindeutiges Temp-Verzeichnis.
    /// Zwei Aufrufe mit demselben <paramref name="fixtureFolderName"/> liefern voneinander unabhaengige
    /// Kopien (kein geteilter Zustand zwischen Leases).
    /// </summary>
    public static IsolatedFixtureLease CopyFixture(
        string solutionRoot, string fixtureFolderName, string tempPrefix = "AiNetTestKit_")
    {
        var sourceRoot = Path.Combine(solutionRoot, "tests", "Fixtures", fixtureFolderName);
        var tempDirectory = TestTempDirectory.Create(tempPrefix);

        CopyDirectory(sourceRoot, tempDirectory.DirectoryPath);

        return new IsolatedFixtureLease(tempDirectory);
    }

    public void Dispose() => tempDir.Dispose();


    private static void CopyDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            if (IsGeneratedPath(relativePath))
            {
                continue;
            }

            var targetFile = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }

    private static bool IsGeneratedPath(string relativePath)
    {
        var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Contains("obj", StringComparer.OrdinalIgnoreCase) ||
               parts.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
