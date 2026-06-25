#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Maps;

public sealed class StructureMapBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public StructureMapBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "StructureMapTests_" + Guid.NewGuid().ToString("N")[..8]);
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
    public void CollectFileInfos_CountsLinesCorrectly()
    {
        CreateTempFiles(("Test.cs", "line1\r\nline2\r\nline3"));
        var infos = StructureMapBuilder.CollectFileInfos(_tempDir);
        Assert.Single(infos);
        Assert.Equal(3, infos[0].Lines);
    }

    [Fact]
    public void CollectFileInfos_ExcludesBinAndObj()
    {
        CreateTempFiles(
            ("bin/Test.cs", "line1"),
            ("obj/Test2.cs", "line1"),
            ("src/Real.cs", "line1"));
        var infos = StructureMapBuilder.CollectFileInfos(_tempDir);
        Assert.Single(infos);
        Assert.Equal("src/Real.cs", infos[0].RelativePath);
    }

    [Fact]
    public void Build_FilesAbove80Percent_ShowsWarning()
    {
        // 420 lines in a 500 max limit file is 84%, which is >= 80% warning threshold.
        var linesContent = string.Join(Environment.NewLine, Enumerable.Repeat("line", 420));
        CreateTempFiles(("BigClass.cs", linesContent));
        var console = new TestLintConsole();
        StructureMapBuilder.Build(_tempDir, 500, console);
        Assert.Contains("⚠ Warnung", console.Output);
    }

    [Fact]
    public void Build_FilesAbove95Percent_ShowsCritical()
    {
        // 480 lines in a 500 max limit file is 96%, which is >= 95% critical threshold.
        var linesContent = string.Join(Environment.NewLine, Enumerable.Repeat("line", 480));
        CreateTempFiles(("CriticalClass.cs", linesContent));
        var console = new TestLintConsole();
        StructureMapBuilder.Build(_tempDir, 500, console);
        Assert.Contains("🔴 Kritisch", console.Output);
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
