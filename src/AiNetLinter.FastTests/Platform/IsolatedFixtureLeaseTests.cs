#nullable enable

using System;
using System.IO;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Platform;

/// <summary>
/// Vertragstests fuer <see cref="IsolatedFixtureLease"/>: belegt mechanisch Kopiervertrag,
/// Isolation zwischen Leases, Dispose-Verhalten und die <c>bin</c>/<c>obj</c>-Auslassung -- reine
/// Datei-I/O gegen eine kopierte <c>BaselineMini</c>, kein MSBuild, kein Prozess.
/// </summary>
[Trait("Category", "Component")]
public sealed class IsolatedFixtureLeaseTests
{
    [Fact]
    public void CopyFixture_ExistingFixture_ReturnsExistingRootPathWithExpectedSourceFiles()
    {
        var solutionRoot = SolutionRootLocator.Find();

        using var lease = IsolatedFixtureLease.CopyFixture(solutionRoot, "BaselineMini");

        Assert.True(Directory.Exists(lease.RootPath));
        Assert.True(File.Exists(Path.Combine(lease.RootPath, "BaselineMini.slnx")));
        Assert.True(File.Exists(Path.Combine(lease.RootPath, "src", "BaselineMini", "BaselineMini.csproj")));
    }

    [Fact]
    public void CopyFixture_CalledTwiceForSameFolder_ReturnsIndependentTempPaths()
    {
        var solutionRoot = SolutionRootLocator.Find();

        using var first = IsolatedFixtureLease.CopyFixture(solutionRoot, "BaselineMini");
        using var second = IsolatedFixtureLease.CopyFixture(solutionRoot, "BaselineMini");

        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.True(Directory.Exists(first.RootPath));
        Assert.True(Directory.Exists(second.RootPath));
    }

    [Fact]
    public void Dispose_DeletesTempDirectory()
    {
        var solutionRoot = SolutionRootLocator.Find();
        var lease = IsolatedFixtureLease.CopyFixture(solutionRoot, "BaselineMini");
        var rootPath = lease.RootPath;

        lease.Dispose();

        Assert.False(Directory.Exists(rootPath));
    }

    [Fact]
    public void CopyFixture_SourceContainsBinAndObjSubfolders_TargetOmitsThem()
    {
        var solutionRoot = SolutionRootLocator.Find();
        using var syntheticSourceRoot = SyntheticSourceWithBinAndObj.Create(solutionRoot);

        using var lease = IsolatedFixtureLease.CopyFixture(syntheticSourceRoot.SolutionRoot, syntheticSourceRoot.FolderName);

        Assert.True(File.Exists(Path.Combine(lease.RootPath, "BaselineMini.slnx")));
        Assert.False(Directory.Exists(Path.Combine(lease.RootPath, "bin")));
        Assert.False(Directory.Exists(Path.Combine(lease.RootPath, "obj")));
    }

    /// <summary>
    /// Simuliert eine Quell-Fixture mit <c>bin</c>/<c>obj</c>-Unterordnern in einer eigenen Kopie von
    /// <c>tests/Fixtures/BaselineMini</c>, damit der Auslassungstest nicht vom zufaelligen Bestandszustand
    /// der echten Fixture abhaengt (die aktuell keinen <c>bin</c>-Ordner enthaelt).
    /// </summary>
    private sealed class SyntheticSourceWithBinAndObj : IDisposable
    {
        private readonly TestTempDirectory tempDir;

        private SyntheticSourceWithBinAndObj(TestTempDirectory tempDir, string folderName)
        {
            this.tempDir = tempDir;
            FolderName = folderName;
        }

        /// <summary>
        /// Wurzelverzeichnis, das wie ein echter <c>solutionRoot</c>-Parameter fuer
        /// <see cref="IsolatedFixtureLease.CopyFixture"/> aussieht (enthaelt <c>tests/Fixtures/&lt;FolderName&gt;</c>).
        /// </summary>
        public string SolutionRoot => tempDir.DirectoryPath;

        public string FolderName { get; }

        public static SyntheticSourceWithBinAndObj Create(string solutionRoot)
        {
            const string folderName = "BaselineMini";
            var tempDir = TestTempDirectory.Create("AiNetSyntheticFixture_");
            try
            {
                var destination = Path.Combine(tempDir.DirectoryPath, "tests", "Fixtures", folderName);

                CopyDirectory(Path.Combine(solutionRoot, "tests", "Fixtures", folderName), destination);

                var binDir = Path.Combine(destination, "bin");
                Directory.CreateDirectory(binDir);
                File.WriteAllText(Path.Combine(binDir, "dummy.dll"), "dummy");

                var objDir = Path.Combine(destination, "obj");
                Directory.CreateDirectory(objDir);
                File.WriteAllText(Path.Combine(objDir, "dummy.cache"), "dummy");

                return new SyntheticSourceWithBinAndObj(tempDir, folderName);
            }
            catch
            {
                tempDir.Dispose();
                throw;
            }
        }

        public void Dispose() => tempDir.Dispose();

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            Directory.CreateDirectory(destinationRoot);
            foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
                var parts = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
                if (Array.Exists(parts, p => p.Equals("obj", StringComparison.OrdinalIgnoreCase) || p.Equals("bin", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var targetFile = Path.Combine(destinationRoot, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(sourceFile, targetFile, overwrite: true);
            }
        }
    }
}
