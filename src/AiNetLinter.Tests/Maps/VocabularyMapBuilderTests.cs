#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Maps;

public sealed class VocabularyMapBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public VocabularyMapBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "VocabularyMapTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateTempFiles(params (string FileName, string Content)[] files)
    {
        foreach (var file in files)
        {
            var path = Path.Combine(_tempDir, file.FileName);
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(path, file.Content);
        }
        return _tempDir;
    }

    [Fact]
    public void ExtractSuffix_PascalCaseWithSuffix_ReturnsLastSegment()
    {
        Assert.Equal("Checker", VocabularyMapBuilder.ExtractSuffix("AsyncVoidChecker"));
        Assert.Equal("Builder", VocabularyMapBuilder.ExtractSuffix("StructureMapBuilder"));
        Assert.Equal("Detector", VocabularyMapBuilder.ExtractSuffix("GeneratedCodeDetector"));
    }

    [Fact]
    public void ExtractSuffix_ShortSingleWord_ReturnsNoSuffix()
    {
        Assert.Equal("(kein Suffix)", VocabularyMapBuilder.ExtractSuffix("Program"));
        Assert.Equal("(kein Suffix)", VocabularyMapBuilder.ExtractSuffix("Foo"));
    }

    [Fact]
    public void ExtractTypeEntries_SimpleClass_ExtractsName()
    {
        CreateTempFiles(("TestClass.cs", "public sealed class MyChecker {}"));
        var files = VocabularyMapBuilder.CollectCsFiles(_tempDir);
        var entries = VocabularyMapBuilder.ExtractTypeEntries(files, _tempDir);
        Assert.Single(entries);
        Assert.Equal("MyChecker", entries[0].Name);
    }

    [Fact]
    public void Build_WithMixedCheckerDetector_EmitsHint()
    {
        CreateTempFiles(
            ("A.cs", "public sealed class FooChecker {}"),
            ("B.cs", "internal sealed class BarDetector {}"));
        var console = new TestLintConsole();
        VocabularyMapBuilder.Build(_tempDir, console);
        Assert.Contains("Gemischte Patterns", console.Output);
    }

    [Fact]
    public void Build_ExcludesBinAndObj()
    {
        CreateTempFiles(
            ("bin/Generated.cs", "public class ShouldNotAppear {}"),
            ("obj/Temp.cs", "public class ShouldNotAppearEither {}"),
            ("Real.cs", "public sealed class RealClass {}"));

        var files = VocabularyMapBuilder.CollectCsFiles(_tempDir).ToList();
        var entries = VocabularyMapBuilder.ExtractTypeEntries(files, _tempDir);

        Assert.Single(entries);
        Assert.Equal("RealClass", entries[0].Name);
    }

    private sealed class TestLintConsole : ILintConsole
    {
        private readonly StringBuilder _sb = new();
        private readonly StringBuilder _errSb = new();

        public string Output => _sb.ToString();
        public string Error => _errSb.ToString();

        public void WriteLine(string message) => _sb.AppendLine(message);
        public void WriteError(string message) => _errSb.AppendLine(message);
    }
}
