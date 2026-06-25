#nullable enable

using System;
using System.IO;
using AiNetLinter.Evals;
using Xunit;

namespace AiNetLinter.Tests.Evals;

public sealed class SpecLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _validFile;
    private readonly string _fileA;
    private readonly string _fileB;

    public SpecLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiNetLinter_SpecLoaderTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _validFile = Path.Combine(_tempDir, "valid.md");
        File.WriteAllText(_validFile, "valid content");

        _fileA = Path.Combine(_tempDir, "spec-a.md");
        File.WriteAllText(_fileA, "content-a");

        _fileB = Path.Combine(_tempDir, "spec-b.md");
        File.WriteAllText(_fileB, "content-b");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_EmptyList_ReturnsFallbackText()
    {
        var result = SpecLoader.Load([]);
        Assert.Contains("Spezifikation fehlt", result);
    }

    [Fact]
    public void Load_SingleFile_ReturnsContent()
    {
        var result = SpecLoader.Load([_validFile]);
        Assert.Contains("valid content", result);
    }

    [Fact]
    public void Load_Directory_ReadsOnlyTopLevelMd()
    {
        // Erstellt: subdir/nested.md
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.md"), "nested content");

        var result = SpecLoader.Load([_tempDir]);
        Assert.Contains("valid content", result);
        Assert.Contains("content-a", result);
        Assert.Contains("content-b", result);
        Assert.DoesNotContain("nested content", result);
    }

    [Fact]
    public void Load_Directory_IgnoresNonMdFiles()
    {
        // Erstellt: notes.txt (ignoriert)
        var txtFile = Path.Combine(_tempDir, "notes.txt");
        File.WriteAllText(txtFile, "txt content");

        var result = SpecLoader.Load([_tempDir]);
        Assert.DoesNotContain("txt content", result);
    }

    [Fact]
    public void Load_MultipleSpecs_ConcatenatesInOrder()
    {
        var result = SpecLoader.Load([_fileA, _fileB]);
        var posA = result.IndexOf("content-a", StringComparison.Ordinal);
        var posB = result.IndexOf("content-b", StringComparison.Ordinal);
        Assert.True(posA < posB);
    }

    [Fact]
    public void Load_NonExistentPath_SkipsGracefully()
    {
        var result = SpecLoader.Load(["C:/does/not/exist.md", _validFile]);
        Assert.Contains("valid content", result);
    }
}
