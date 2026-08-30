#nullable enable

using System;
using System.IO;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal static class ExternalSourceRepositoryCacheCleanup
{
    internal static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)
                && !ExternalSourceRepositoryPathGuard.ContainsReparsePointOnPath(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ignored) when (ExternalSourceRepositoryCacheStorage.IsCacheException(ignored))
        {
        }
    }
}
