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

internal sealed class RecordingLintConsole : ILintConsole
{
    private readonly List<string> output = [];
    private readonly List<string> errors = [];

    public string OutputText => string.Join("\n", output);

    public string ErrorText => string.Join("\n", errors);

    public IReadOnlyList<string> Errors => errors;

    public IReadOnlyList<string> ErrorLines => errors;

    public void WriteLine(string message) => output.Add(message);

    public void WriteError(string message) => errors.Add(message);
}

internal sealed class TestTempDirectory : IDisposable
{
    private TestTempDirectory(string directoryPath) => DirectoryPath = directoryPath;

    public string DirectoryPath { get; }

    public static TestTempDirectory Create(string prefix) => new(Directory.CreateTempSubdirectory(prefix).FullName);

    public string CreateFile(string relativePath, string content = "")
    {
        var path = Path.Combine(DirectoryPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => TestHelper.TryDeleteDirectoryRecursive(DirectoryPath);

    public static implicit operator string(TestTempDirectory directory) => directory.DirectoryPath;
}
