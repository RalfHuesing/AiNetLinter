#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Maps;

public sealed class HotspotMapBuilderTests : IDisposable
{
    private readonly string _tempDir;

    public HotspotMapBuilderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "HotspotMapTests_" + Guid.NewGuid().ToString("N")[..8]);
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
    public void Build_NoHotspots_ShowsGreenMessage()
    {
        CreateTempFiles(("Small.cs", "namespace Foo;"));
        var console = new TestLintConsole();
        HotspotMapBuilder.Build(_tempDir, 500, console);
        Assert.Contains("grünen Bereich", console.Output);
    }

    [Fact]
    public void Build_CriticalFile_ShowsCriticalSection()
    {
        // 476 lines in a 500 max limit file is 95.2% (critical limit >= 95%)
        var linesContent = string.Join(Environment.NewLine, Enumerable.Repeat("line", 476));
        CreateTempFiles(("Critical.cs", linesContent));
        var console = new TestLintConsole();
        HotspotMapBuilder.Build(_tempDir, 500, console);
        Assert.Contains("Kritische Dateien", console.Output);
        Assert.Contains("Critical.cs", console.Output);
    }

    [Fact]
    public void Build_WarningFile_ShowsWarningSection()
    {
        // 410 lines in a 500 max limit file is 82% (warning limit >= 80% and < 95%)
        var linesContent = string.Join(Environment.NewLine, Enumerable.Repeat("line", 410));
        CreateTempFiles(("Warning.cs", linesContent));
        var console = new TestLintConsole();
        HotspotMapBuilder.Build(_tempDir, 500, console);
        Assert.Contains("Warnungs-Dateien", console.Output);
        Assert.Contains("Warning.cs", console.Output);
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
