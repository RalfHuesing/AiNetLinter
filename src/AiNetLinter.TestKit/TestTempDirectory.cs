#nullable enable

using System;
using System.IO;

namespace AiNetLinter.TestKit;

/// <summary>
/// Verwaltet ein isoliertes temporäres Unterverzeichnis innerhalb des Projektmappen-Temp-Ordners (<c>&lt;RepoRoot&gt;/temp/</c>).
/// Implementiert <see cref="IDisposable"/>, um das erzeugte Testverzeichnis beim Teardown automatisch zu bereinigen.
/// </summary>
public sealed class TestTempDirectory : IDisposable
{
    private const string DefaultPrefix = "ainet-test-";
    private const string TempFolderName = "temp";
    private bool disposed;

    private TestTempDirectory(string directoryPath)
    {
        DirectoryPath = directoryPath;
    }

    /// <summary>
    /// Absoluter Pfad zum temporären Testverzeichnis.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    /// Absoluter Pfad zum Wurzel-Temp-Verzeichnis des Repositories (<c>&lt;RepoRoot&gt;/temp/</c>).
    /// </summary>
    public static string RootTempDirectory => Path.Combine(SolutionRootLocator.Find(), TempFolderName);

    /// <summary>
    /// Erstellt ein neues, eindeutiges Unterverzeichnis im Projektmappen-Temp-Ordner.
    /// </summary>
    /// <param name="prefix">Optionales Namenspräfix für das Unterverzeichnis.</param>
    /// <returns>Eine neue <see cref="TestTempDirectory"/>-Instanz.</returns>
    public static TestTempDirectory Create(string prefix = DefaultPrefix)
    {
        var root = RootTempDirectory;
        Directory.CreateDirectory(root);

        var subDirName = $"{prefix}{Guid.NewGuid():N}";
        var fullPath = Path.Combine(root, subDirName);
        Directory.CreateDirectory(fullPath);

        return new TestTempDirectory(fullPath);
    }

    /// <summary>
    /// Erstellt eine Datei mit relativem Pfad und Inhalt im temporären Verzeichnis und gibt den absoluten Pfad zurück.
    /// </summary>
    public string CreateFile(string relativePath, string content = "")
    {
        var path = GetPath(relativePath);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Erstellt ein Unterverzeichnis mit relativem Pfad im temporären Verzeichnis und gibt den absoluten Pfad zurück.
    /// </summary>
    public string CreateSubdirectory(string relativePath)
    {
        var path = GetPath(relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Liefert einen absoluten Pfad für eine relative Datei- oder Ordnerangabe innerhalb dieses Temp-Verzeichnisses.
    /// </summary>
    public string GetPath(string relativePath) => Path.GetFullPath(Path.Combine(DirectoryPath, relativePath));

    /// <summary>
    /// Implizite Konvertierung zu <see cref="string"/>, damit Instanzen direkt an Methoden mit Pfad-Parametern übergeben werden können.
    /// </summary>
    public static implicit operator string(TestTempDirectory directory) => directory.DirectoryPath;

    public override string ToString() => DirectoryPath;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        TryDeleteDirectory(DirectoryPath);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Transient Windows file lock / teardown resilience
        }
        catch (UnauthorizedAccessException)
        {
            // Teardown resilience
        }
    }
}
