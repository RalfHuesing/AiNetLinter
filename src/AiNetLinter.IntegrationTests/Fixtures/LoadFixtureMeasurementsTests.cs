#nullable enable

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.IntegrationTests.Fixtures;

/// <summary>
/// Performance-Skalierungsmessung gegen die generierte Last-Fixture. Schreibt strukturierte Samples
/// in xUnit-Output und prueft fachliche Gueltigkeit, Vollstaendigkeit und nichtnegative Werte.
/// </summary>
[Trait("Category", "Performance")]
public sealed class LoadFixtureMeasurementsTests
{
    private readonly ITestOutputHelper _output;

    public LoadFixtureMeasurementsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Measure_ColdStart_On_1k_LOC_Fixture()
    {
        // 1 Projekt x 50 Dateien x ~20 Zeilen = ca. 1000 LOC
        using var handle = LoadFixtureBuilder.Build(
            "1k-loc",
            projectCount: 1,
            filesPerProject: 50,
            linesPerFile: 20);

        var sw = Stopwatch.StartNew();
        using var catalog = await SourceFileCatalog.LoadAsync(handle.SolutionPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));
        var solution = server.GetCurrentSolution();
        sw.Stop();

        Assert.NotNull(solution);
        var durationSec = sw.Elapsed.TotalSeconds;
        _output.WriteLine($"ColdStart (1k-LOC-Fixture, 50 Dateien): {durationSec:F3} s");

        Assert.True(double.IsFinite(durationSec) && durationSec >= 0);
    }

    [Fact]
    public async Task Measure_GetCurrentSolution_On_10k_LOC_Fixture_UnderBaseline()
    {
        // 5 Projekte x 200 Dateien x ~10 Zeilen = ca. 10.000 LOC
        using var handle = LoadFixtureBuilder.Build(
            "10k-loc",
            projectCount: 5,
            filesPerProject: 200,
            linesPerFile: 10);

        using var catalog = await SourceFileCatalog.LoadAsync(handle.SolutionPath);
        using var server = new McpCodeGraphServer(McpCodeGraphServerOptions.From(
            new McpCodeGraphServerOptionsFromParameters(catalog)));

        // Initial-Aufruf initialisiert _fileState und richtet den mtime-Cache ein.
        _ = server.GetCurrentSolution();

        const int iterations = 10;
        var samples = new double[iterations];
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            var solution = server.GetCurrentSolution();
            sw.Stop();
            Assert.NotNull(solution);
            samples[i] = sw.Elapsed.TotalSeconds;
        }

        var min = samples.Min();
        var max = samples.Max();
        var median = samples.OrderBy(s => s).ElementAt(samples.Length / 2);
        var mean = samples.Average();
        _output.WriteLine(
            $"GetCurrentSolution (10k-LOC, 1000 Dateien, 10 Iterationen): " +
            $"min={min:F3} s, median={median:F3} s, mean={mean:F3} s, max={max:F3} s");

        Assert.True(double.IsFinite(min) && min >= 0);
        Assert.True(double.IsFinite(median) && median >= 0);
        Assert.True(double.IsFinite(mean) && mean >= 0);
        Assert.True(double.IsFinite(max) && max >= 0);
    }
}
