#nullable enable

using System;
using System.IO;
using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.Diagnostics;
using Xunit;

namespace AiNetLinter.Tests.Diagnostics;

// @covers PerformanceProfiler
// @covers DocumentPerformanceEntry
// @covers ProfilerJsonReport
// @covers ProfilerSummary
public sealed class PerformanceProfilerTests
{
    [Fact]
    public void LinterConfigLoader_LoadsEnablePerformanceProfiling()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + "_config.json");
        var json = """
            {
                "global": {
                    "enablePerformanceProfiling": false
                },
                "metrics": {}
            }
            """;
        File.WriteAllText(tempPath, json);
        try
        {
            var result = LinterConfigLoader.TryLoadConfig(tempPath, isRequired: false);
            Assert.NotNull(result);
            Assert.False(result.Global.EnablePerformanceProfiling);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void PerformanceProfiler_Disabled_DoesNotWriteReport()
    {
        var profiler = new PerformanceProfiler(false);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var measurementsDir = Path.Combine(baseDir, "measurements");

        if (Directory.Exists(measurementsDir))
        {
            Directory.Delete(measurementsDir, true);
        }

        profiler.StartPhase("TestPhase");
        profiler.StopPhase("TestPhase");
        profiler.RecordDocumentAnalysis("file.cs", 10.5, 1);
        profiler.RecordPostAnalysisStep("PostStep", 5.0);

        profiler.WriteReport("testTarget", null);

        Assert.False(Directory.Exists(measurementsDir));
    }

    [Fact]
    public void PerformanceProfiler_Enabled_WritesReportWithRulesPathAndArguments()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var measurementsDir = Path.Combine(baseDir, "measurements");

        if (Directory.Exists(measurementsDir))
        {
            Directory.Delete(measurementsDir, true);
        }

        var mockArgs = new[] { "--path", "testProj", "--config", "rules.json" };
        var profiler = new PerformanceProfiler(true, mockArgs);

        profiler.StartPhase("TestPhase");
        profiler.StopPhase("TestPhase");
        profiler.RecordDocumentAnalysis("testFile.cs", 15.0, 2);

        profiler.WriteReport("testProj", "C:\\mock\\solution.sln", "C:\\mock\\rules.json");

        Assert.True(Directory.Exists(measurementsDir));

        var logFiles = Directory.GetFiles(measurementsDir, "performance.log", SearchOption.AllDirectories);
        var jsonFiles = Directory.GetFiles(measurementsDir, "performance.json", SearchOption.AllDirectories);

        Assert.Single(logFiles);
        Assert.Single(jsonFiles);

        var logContent = File.ReadAllText(logFiles[0]);
        var jsonContent = File.ReadAllText(jsonFiles[0]);

        Assert.Contains("Solution File: C:\\mock\\solution.sln", logContent);
        Assert.Contains("Rules File: C:\\mock\\rules.json", logContent);
        Assert.Contains("Arguments: --path testProj --config rules.json", logContent);

        var report = JsonSerializer.Deserialize<ProfilerJsonReport>(jsonContent);
        Assert.NotNull(report);
        Assert.Equal("C:\\mock\\solution.sln", report.SolutionPath);
        Assert.Equal("C:\\mock\\rules.json", report.RulesPath);
        Assert.Equal("--path testProj --config rules.json", report.Arguments);

        Directory.Delete(measurementsDir, true);
    }
}
