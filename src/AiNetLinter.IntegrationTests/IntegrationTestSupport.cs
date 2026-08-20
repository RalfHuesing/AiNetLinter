#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.IntegrationTests;

internal static class TestHelper
{
    public static Config CreateDefaultConfig() => new() { Global = new GlobalConfig(), Metrics = new MetricsConfig() };

    public static void DeleteFileIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void TryDeleteDirectoryRecursive(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine(exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine(exception.Message);
        }
    }

    public static void TryDeleteLogFileAndDirectory(string path)
    {
        DeleteFileIfExists(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            TryDeleteDirectoryRecursive(directory);
        }
    }
}

