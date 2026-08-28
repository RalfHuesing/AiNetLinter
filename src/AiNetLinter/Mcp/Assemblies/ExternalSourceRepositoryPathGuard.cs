#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies;

internal static class ExternalSourceRepositoryPathGuard
{
    internal static bool IsDescendantPath(string root, string candidate)
    {
        try
        {
            var fullRoot = Path.GetFullPath(root);
            var fullCandidate = Path.GetFullPath(candidate);
            var rootPrefix = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? fullRoot
                : fullRoot + Path.DirectorySeparatorChar;

            return !string.Equals(fullRoot, fullCandidate, StringComparison.OrdinalIgnoreCase)
                && fullCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    internal static bool ContainsReparsePointOnPath(string path)
    {
        try
        {
            var current = Path.GetFullPath(path);
            while (current is not null)
            {
                if (!TryGetAttributes(current, out var attributes, out var exists) || !exists)
                {
                    return true;
                }

                if (IsReparsePointAttribute(attributes))
                {
                    return true;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            return false;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
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
            if (!TryInspectDirectory(pending.Pop(), pending))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryInspectDirectory(string directory, Stack<string> pending)
    {
        if (!TryGetAttributes(directory, out var directoryAttributes, out var directoryExists)
            || !directoryExists
            || IsReparsePointAttribute(directoryAttributes))
        {
            return false;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                if (!TryGetAttributes(entry, out var attributes, out var exists)
                    || !exists
                    || IsReparsePointAttribute(attributes))
                {
                    return false;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pending.Push(entry);
                }
            }

            return true;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    internal static bool IsReparsePointAttribute(FileAttributes attributes) =>
        attributes.HasFlag(FileAttributes.ReparsePoint);

    internal static bool IsOwnedCheckout(ExternalSourceCheckoutOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        return IsDescendantPath(ownership.StagingRoot, ownership.CheckoutPath)
            && !ContainsReparsePointOnPath(ownership.StagingRoot)
            && !ContainsReparsePointOnPath(ownership.CheckoutPath)
            && !ContainsReparsePointOnPath(ownership.OwnershipMarkerPath)
            && TryGetAttributes(ownership.CheckoutPath, out var attributes, out var exists)
            && exists
            && attributes.HasFlag(FileAttributes.Directory)
            && ownership.HasValidToken();
    }

    internal static bool TryDeleteOwnedCheckout(ExternalSourceCheckoutOwnership ownership)
    {
        ArgumentNullException.ThrowIfNull(ownership);
        try
        {
            return IsOwnedCheckout(ownership) && TryDeleteEntry(ownership.CheckoutPath);
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            return false;
        }
    }

    private static bool TryDeleteEntry(string path)
    {
        if (!TryGetAttributes(path, out var attributes, out var exists))
        {
            return false;
        }

        if (!exists)
        {
            return true;
        }

        if (IsReparsePointAttribute(attributes))
        {
            return TryDeleteReparsePoint(path, attributes);
        }

        if (attributes.HasFlag(FileAttributes.Directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(path))
            {
                if (!TryDeleteEntry(entry))
                {
                    return false;
                }
            }

            Directory.Delete(path);
            return TryGetAttributes(path, out _, out var stillExists) && !stillExists;
        }

        File.Delete(path);
        return TryGetAttributes(path, out _, out var fileStillExists) && !fileStillExists;
    }

    private static bool TryDeleteReparsePoint(string path, FileAttributes attributes)
    {
        if (attributes.HasFlag(FileAttributes.Directory))
        {
            Directory.Delete(path);
        }
        else
        {
            File.Delete(path);
        }

        return TryGetAttributes(path, out _, out var stillExists) && !stillExists;
    }

    private static bool TryGetAttributes(
        string path,
        out FileAttributes attributes,
        out bool exists)
    {
        try
        {
            attributes = File.GetAttributes(path);
            exists = true;
            return true;
        }
        catch (FileNotFoundException)
        {
            attributes = default;
            exists = false;
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            attributes = default;
            exists = false;
            return true;
        }
        catch (Exception exception) when (
            ExternalSourceRepositoryFailurePolicy.IsFileSystemException(exception))
        {
            attributes = default;
            exists = false;
            return false;
        }
    }
}
