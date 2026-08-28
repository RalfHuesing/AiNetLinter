#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryPathGuard
{
    internal static bool IsDescendantPath(string root, string candidate)
    {
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            || root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? root
            : root + Path.DirectorySeparatorChar;
        var fullCandidate = Path.GetFullPath(candidate);
        return !string.Equals(root, fullCandidate, StringComparison.OrdinalIgnoreCase)
            && fullCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsReparsePointOnPath(string path)
    {
        try
        {
            for (var current = new DirectoryInfo(path); current is not null; current = current.Parent)
            {
                if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return true;
        }
    }

    internal static bool ContainsReparsePointInTree(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (HasReparsePoint(directory))
            {
                return true;
            }

            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (HasReparsePoint(entry))
                    {
                        return true;
                    }

                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                    }
                }
            }
            catch (Exception exception) when (IsFileSystemException(exception))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsReparsePointAttribute(FileAttributes attributes) =>
        attributes.HasFlag(FileAttributes.ReparsePoint);

    internal static bool TryDeleteOwnedCheckout(string stagingRoot, string checkoutPath)
    {
        try
        {
            return IsDescendantPath(stagingRoot, checkoutPath)
                && !ContainsReparsePointOnPath(stagingRoot)
                && TryDeleteEntry(checkoutPath);
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool TryDeleteEntry(string path)
    {
        var isReparsePoint = HasReparsePoint(path);
        if (!Directory.Exists(path) && !File.Exists(path) && !isReparsePoint)
        {
            return true;
        }

        if (isReparsePoint)
        {
            return TryDeleteReparsePoint(path);
        }

        if (Directory.Exists(path))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (!TryDeleteEntry(entry))
                {
                    return false;
                }
            }

            Directory.Delete(path);
            return !Directory.Exists(path);
        }

        File.Delete(path);
        return !File.Exists(path);
    }

    private static bool TryDeleteReparsePoint(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path);
            return !Directory.Exists(path);
        }

        File.Delete(path);
        return !File.Exists(path);
    }

    private static bool HasReparsePoint(string path)
    {
        try
        {
            return IsReparsePointAttribute(File.GetAttributes(path));
        }
        catch (Exception exception) when (IsFileSystemException(exception))
        {
            return true;
        }
    }

    private static bool IsFileSystemException(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or NotSupportedException;
}
