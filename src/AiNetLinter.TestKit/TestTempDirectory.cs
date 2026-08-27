#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AiNetLinter.TestKit;

/// <summary>
/// Verwaltet ein isoliertes temporäres Unterverzeichnis innerhalb des Projektmappen-Temp-Ordners (<c>&lt;RepoRoot&gt;/temp/</c>).
/// Implementiert <see cref="IDisposable"/>, um das erzeugte Testverzeichnis beim Teardown automatisch zu bereinigen.
/// </summary>
public sealed class TestTempDirectory : IDisposable
{
    private const string DefaultPrefix = "ainet-test-";
    private const string TempFolderName = "temp";
    private const string OwnerMarkerFilePrefix = ".ainet-test-owner-";
    private static readonly TimeSpan StaleDirectoryAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan[] DeleteRetryDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromMilliseconds(25),
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
    ];
    private static readonly ConcurrentDictionary<string, FileStream> ActiveDirectories = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    static TestTempDirectory()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupActiveDirectories();
    }

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
        CleanupStaleDirectories(root);

        var subDirName = $"{prefix}{Guid.NewGuid():N}";
        var fullPath = Path.Combine(root, subDirName);
        Directory.CreateDirectory(fullPath);

        try
        {
            var ownerMarker = CreateOwnerMarker(fullPath);
            if (!ActiveDirectories.TryAdd(fullPath, ownerMarker))
            {
                ownerMarker.Dispose();
                throw new IOException($"Temporäres Testverzeichnis wurde doppelt registriert: {fullPath}");
            }

            return new TestTempDirectory(fullPath);
        }
        catch
        {
            if (ActiveDirectories.TryRemove(fullPath, out var ownerMarker))
            {
                DisposeOwnerMarker(ownerMarker);
            }

            DeleteOwnerMarker(fullPath);
            TryDeleteDirectory(fullPath);
            throw;
        }
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
        if (ActiveDirectories.TryRemove(DirectoryPath, out var ownerMarker))
        {
            DisposeOwnerMarker(ownerMarker);
        }

        DeleteOwnerMarker(DirectoryPath);
        TryDeleteDirectory(DirectoryPath);
    }

    private static FileStream CreateOwnerMarker(string directoryPath)
    {
        var markerPath = GetOwnerMarkerPath(directoryPath);
        var marker = new FileStream(
            markerPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 256,
            options: FileOptions.SequentialScan);

        try
        {
            var metadata = Encoding.UTF8.GetBytes(
                $"pid={Environment.ProcessId};createdUtc={DateTime.UtcNow:O}{Environment.NewLine}");
            marker.Write(metadata, 0, metadata.Length);
            marker.Flush(flushToDisk: true);
            return marker;
        }
        catch
        {
            marker.Dispose();
            throw;
        }
    }

    private static void CleanupStaleDirectories(string root)
    {
        IEnumerable<string> directories;
        try
        {
            directories = Directory.EnumerateDirectories(root).ToArray();
        }
        catch (IOException)
        {
            return;
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }

        var staleBeforeUtc = DateTime.UtcNow - StaleDirectoryAge;
        foreach (var directory in directories)
        {
            if (!ActiveDirectories.ContainsKey(directory))
            {
                CleanupStaleDirectory(directory, staleBeforeUtc);
            }
        }
    }

    private static void CleanupStaleDirectory(string directory, DateTime staleBeforeUtc)
    {
        var markerPath = GetOwnerMarkerPath(directory);
        if (File.Exists(markerPath))
        {
            CleanupMarkerOwnedDirectory(directory, markerPath);
            return;
        }

        if (LooksLikeTestDirectory(directory) && GetLastWriteTimeUtc(directory) < staleBeforeUtc)
        {
            TryDeleteDirectory(directory);
        }
    }

    private static void CleanupMarkerOwnedDirectory(string directory, string markerPath)
    {
        if (!CanAcquireOwnerMarker(markerPath))
        {
            return;
        }

        if (TryDeleteDirectory(directory))
        {
            DeleteOwnerMarker(directory);
        }
    }

    private static bool CanAcquireOwnerMarker(string markerPath)
    {
        try
        {
            using var probe = new FileStream(markerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string GetOwnerMarkerPath(string directoryPath)
    {
        var parentPath = Directory.GetParent(directoryPath)?.FullName
            ?? throw new ArgumentException("Temporäres Testverzeichnis muss einen Elternpfad besitzen.", nameof(directoryPath));
        return Path.Combine(parentPath, OwnerMarkerFilePrefix + Path.GetFileName(directoryPath));
    }

    private static void DeleteOwnerMarker(string directoryPath)
    {
        var markerPath = GetOwnerMarkerPath(directoryPath);
        for (var attempt = 0; attempt < DeleteRetryDelays.Length; attempt++)
        {
            try
            {
                if (!File.Exists(markerPath))
                {
                    return;
                }

                File.SetAttributes(markerPath, FileAttributes.Normal);
                File.Delete(markerPath);
                return;
            }
            catch (IOException) when (attempt < DeleteRetryDelays.Length - 1)
            {
                Thread.Sleep(DeleteRetryDelays[attempt + 1]);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryDelays.Length - 1)
            {
                Thread.Sleep(DeleteRetryDelays[attempt + 1]);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    private static bool LooksLikeTestDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return name.Length >= 32 && Guid.TryParseExact(name[^32..], "N", out _);
    }

    private static DateTime GetLastWriteTimeUtc(string path)
    {
        try
        {
            return Directory.GetLastWriteTimeUtc(path);
        }
        catch (IOException)
        {
            return DateTime.UtcNow;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTime.UtcNow;
        }
    }

    private static void CleanupActiveDirectories()
    {
        foreach (var pair in ActiveDirectories.ToArray())
        {
            if (ActiveDirectories.TryRemove(pair.Key, out var ownerMarker))
            {
                DisposeOwnerMarker(ownerMarker);
            }

            DeleteOwnerMarker(pair.Key);
            TryDeleteDirectory(pair.Key);
        }
    }

    private static void DisposeOwnerMarker(FileStream ownerMarker)
    {
        try
        {
            ownerMarker.Dispose();
        }
        catch (IOException)
        {
            // Best-effort cleanup; the directory janitor will retry later.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; the directory janitor will retry later.
        }
    }

    private static bool TryDeleteDirectory(string path)
    {
        for (var attempt = 0; attempt < DeleteRetryDelays.Length; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return true;
                }

                NormalizeAttributes(path);
                Directory.Delete(path, recursive: true);
                return !Directory.Exists(path);
            }
            catch (IOException) when (attempt < DeleteRetryDelays.Length - 1)
            {
                Thread.Sleep(DeleteRetryDelays[attempt + 1]);
            }
            catch (UnauthorizedAccessException) when (attempt < DeleteRetryDelays.Length - 1)
            {
                Thread.Sleep(DeleteRetryDelays[attempt + 1]);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        return false;
    }

    private static void NormalizeAttributes(string path)
    {
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(entry, FileAttributes.Normal);
                }
                catch (IOException)
                {
                    // The following delete attempt reports the actual lock if it remains.
                }
                catch (UnauthorizedAccessException)
                {
                    // The following delete attempt reports the actual access problem.
                }
            }

            File.SetAttributes(path, FileAttributes.Normal);
        }
        catch (IOException)
        {
            // The following delete attempt reports the actual filesystem problem.
        }
        catch (UnauthorizedAccessException)
        {
            // The following delete attempt reports the actual filesystem problem.
        }
    }
}
