#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Diagnostics;

/// <summary>
/// Verwaltet Zeitmessungen für verschiedene Linter-Phasen und generiert Protokollberichte.
/// </summary>
public sealed class PerformanceProfiler : IPerformanceProfiler
{
    private readonly bool _enabled;
    private readonly string? _arguments;

    private readonly Stopwatch _totalStopwatch = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _phaseStopwatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _phaseDurations = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentBag<DocumentPerformanceEntry> _documentEntries = new();
    private readonly ConcurrentDictionary<string, double> _postAnalysisStepDurations = new(StringComparer.OrdinalIgnoreCase);

    internal PerformanceProfiler(bool enabled, string[]? args = null)
    {
        _enabled = enabled;

        if (args != null)
        {
            _arguments = string.Join(" ", args);
        }
        else
        {
            try
            {
                var cmdArgs = Environment.GetCommandLineArgs();
                if (cmdArgs.Length > 1)
                {
                    _arguments = string.Join(" ", cmdArgs.Skip(1));
                }
            }
            catch (Exception ignored)
            {
                _ = ignored;
            }
        }

        if (_enabled)
        {
            _totalStopwatch.Start();
        }
    }

    /// <summary>
    /// Startet oder startet eine bestimmte Ausführungsphase neu.
    /// </summary>
    public void StartPhase(string phaseName)
    {
        if (!_enabled) return;
        var sw = _phaseStopwatches.GetOrAdd(phaseName, _ => new Stopwatch());
        sw.Restart();
    }

    /// <summary>
    /// Stoppt eine Phase und zeichnet deren Dauer auf.
    /// </summary>
    public void StopPhase(string phaseName)
    {
        if (!_enabled) return;
        if (_phaseStopwatches.TryGetValue(phaseName, out var sw))
        {
            sw.Stop();
            _phaseDurations[phaseName] = sw.Elapsed.TotalMilliseconds;
        }
    }

    /// <summary>
    /// Zeichnet die Performance-Daten für die Analyse einer einzelnen Datei auf.
    /// </summary>
    public void RecordDocumentAnalysis(string filePath, double durationMs, int violationsCount)
    {
        if (!_enabled) return;
        _documentEntries.Add(new DocumentPerformanceEntry
        {
            FilePath = filePath,
            DurationMs = durationMs,
            ViolationsCount = violationsCount
        });
    }

    /// <summary>
    /// Zeichnet die Dauer eines spezifischen Post-Analysis-Schritts auf.
    /// </summary>
    public void RecordPostAnalysisStep(string stepName, double durationMs)
    {
        if (!_enabled) return;
        _postAnalysisStepDurations[stepName] = durationMs;
    }

    private sealed record ProfilerContext(
        string SolutionName,
        string TargetPath,
        string? SolutionFilePath,
        string? AbsoluteRulesPath,
        DateTime Timestamp);

    private sealed record PhaseDurationSnapshot
    {
        public double WorkspaceLoadMs { get; init; }
        public double AutoFixMs { get; init; }
        public double DocumentAnalysisMs { get; init; }
        public double PostAnalysisMs { get; init; }
        public double OptionalOutputsMs { get; init; }
        public double OutputWritingMs { get; init; }
    }

    /// <summary>
    /// Generiert die Berichte im base-Verzeichnis unter measurements/ (wenn aktiviert).
    /// </summary>
    public void WriteReport(string targetPath, string? solutionFilePath, string? rulesFilePath = null)
    {
        if (!_enabled) return;
        _totalStopwatch.Stop();
        try
        {
            var ctx = new ProfilerContext(
                ResolveSolutionName(targetPath, solutionFilePath),
                targetPath, solutionFilePath,
                ResolveAbsoluteRulesPath(rulesFilePath),
                DateTime.Now);
            var targetDir = SetupTargetDirectory(ctx);
            var phases = CollectPhaseDurations();
            var totalMs = _totalStopwatch.Elapsed.TotalMilliseconds;
            WriteJsonFile(targetDir, BuildProfilerReport(ctx, phases, totalMs));
            WriteLogFile(targetDir, ctx, phases, totalMs);
            Console.WriteLine($"[INFO]: Performance-Messdaten erzeugt unter: {targetDir}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Schreiben des Performance-Reports: {ex.Message}");
        }
    }

    private static string ResolveSolutionName(string targetPath, string? solutionFilePath)
    {
        if (!string.IsNullOrEmpty(solutionFilePath))
            return Path.GetFileNameWithoutExtension(solutionFilePath);
        if (!string.IsNullOrEmpty(targetPath))
        {
            var cleanedPath = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var name = Path.GetFileName(cleanedPath);
            return string.IsNullOrEmpty(name) ? "TargetProject" : name;
        }
        return "UnknownProject";
    }

    private static string? ResolveAbsoluteRulesPath(string? rulesFilePath)
    {
        if (string.IsNullOrEmpty(rulesFilePath)) return null;
        try { return Path.GetFullPath(rulesFilePath); }
        catch (Exception ignored) { _ = ignored; return rulesFilePath; }
    }

    private static string SetupTargetDirectory(ProfilerContext ctx)
    {
        var dirName = $"{ctx.SolutionName}-{ctx.Timestamp:yyyy-MM-dd-HH-mm-ss-fff}-{Guid.NewGuid().ToString("N")[..8]}";
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var targetDir = Path.Combine(baseDir, "measurements", ctx.SolutionName, ctx.Timestamp.ToString("yyyy-MM-dd"), dirName);
        Directory.CreateDirectory(targetDir);
        return targetDir;
    }

    private PhaseDurationSnapshot CollectPhaseDurations()
    {
        _phaseDurations.TryGetValue("WorkspaceLoading", out var workspaceLoadMs);
        _phaseDurations.TryGetValue("AutoFix", out var autoFixMs);
        _phaseDurations.TryGetValue("DocumentAnalysis", out var documentAnalysisMs);
        _phaseDurations.TryGetValue("PostAnalysis", out var postAnalysisMs);
        _phaseDurations.TryGetValue("OptionalOutputs", out var optionalOutputsMs);
        _phaseDurations.TryGetValue("OutputWriting", out var outputWritingMs);
        return new PhaseDurationSnapshot
        {
            WorkspaceLoadMs = workspaceLoadMs,
            AutoFixMs = autoFixMs,
            DocumentAnalysisMs = documentAnalysisMs,
            PostAnalysisMs = postAnalysisMs,
            OptionalOutputsMs = optionalOutputsMs,
            OutputWritingMs = outputWritingMs
        };
    }

    private ProfilerJsonReport BuildProfilerReport(ProfilerContext ctx, PhaseDurationSnapshot phases, double totalMs)
    {
        var documentCount = _documentEntries.Count;
        var totalViolations = _documentEntries.Sum(d => d.ViolationsCount);
        var avgDocMs = documentCount > 0 ? _documentEntries.Average(d => d.DurationMs) : 0.0;
        return new ProfilerJsonReport
        {
            SolutionName = ctx.SolutionName,
            SolutionPath = ctx.SolutionFilePath ?? ctx.TargetPath,
            RulesPath = ctx.AbsoluteRulesPath,
            Arguments = _arguments,
            Timestamp = ctx.Timestamp.ToString("o"),
            Summary = new ProfilerSummary
            {
                TotalDurationMs = totalMs,
                WorkspaceLoadDurationMs = phases.WorkspaceLoadMs,
                AutoFixDurationMs = phases.AutoFixMs,
                AnalysisDurationMs = phases.DocumentAnalysisMs,
                PostAnalysisDurationMs = phases.PostAnalysisMs,
                OptionalOutputsDurationMs = phases.OptionalOutputsMs,
                OutputWritingDurationMs = phases.OutputWritingMs,
                DocumentCount = documentCount,
                TotalViolationsCount = totalViolations,
                AverageDocumentAnalysisDurationMs = avgDocMs
            },
            PostAnalysisSteps = _postAnalysisStepDurations.ToDictionary(k => k.Key, v => v.Value),
            Documents = _documentEntries.OrderByDescending(d => d.DurationMs).ToList()
        };
    }

    private static void WriteJsonFile(string targetDir, ProfilerJsonReport report)
    {
        var jsonPath = Path.Combine(targetDir, "performance.json");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions), Encoding.UTF8);
    }

    private void WriteLogFile(string targetDir, ProfilerContext ctx, PhaseDurationSnapshot phases, double totalMs)
    {
        var logPath = Path.Combine(targetDir, "performance.log");
        using var writer = new StreamWriter(logPath, false, Encoding.UTF8);
        writer.WriteLine($"=== PERFORMANCE LOG: {ctx.SolutionName} ===");
        writer.WriteLine($"Timestamp: {ctx.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        writer.WriteLine($"Target Path: {ctx.TargetPath}");
        if (!string.IsNullOrEmpty(ctx.SolutionFilePath))
            writer.WriteLine($"Solution File: {ctx.SolutionFilePath}");
        if (!string.IsNullOrEmpty(ctx.AbsoluteRulesPath))
            writer.WriteLine($"Rules File: {ctx.AbsoluteRulesPath}");
        if (!string.IsNullOrEmpty(_arguments))
            writer.WriteLine($"Arguments: {_arguments}");
        writer.WriteLine();
        writer.WriteLine("--- Phases ---");
        WritePhaseLine(writer, "Workspace Loading", phases.WorkspaceLoadMs, totalMs);
        WritePhaseLine(writer, "Auto-Fix Execution", phases.AutoFixMs, totalMs);
        WritePhaseLine(writer, "Document Analysis (overall)", phases.DocumentAnalysisMs, totalMs);
        WritePhaseLine(writer, "Post-Analysis Checks (overall)", phases.PostAnalysisMs, totalMs);
        WritePhaseLine(writer, "Optional Outputs (Playbook/Graph)", phases.OptionalOutputsMs, totalMs);
        WritePhaseLine(writer, "Output Writing", phases.OutputWritingMs, totalMs);
        writer.WriteLine();
        if (_postAnalysisStepDurations.Count > 0)
        {
            writer.WriteLine("--- Post-Analysis Steps ---");
            foreach (var step in _postAnalysisStepDurations.OrderByDescending(s => s.Value))
                writer.WriteLine($"  {step.Key,-30}: {step.Value,10:F2} ms");
            writer.WriteLine();
        }
        if (_documentEntries.Count > 0)
        {
            writer.WriteLine("--- Top 20 Slowest Documents ---");
            foreach (var doc in _documentEntries.OrderByDescending(d => d.DurationMs).Take(20))
                writer.WriteLine($"  {doc.DurationMs,10:F2} ms | Violations: {doc.ViolationsCount,3} | {doc.FilePath}");
            writer.WriteLine();
        }
        writer.WriteLine("=== SUMMARY ===");
        var docCount = _documentEntries.Count;
        var avgMs = docCount > 0 ? _documentEntries.Average(d => d.DurationMs) : 0.0;
        writer.WriteLine($"- Total Run Duration    : {totalMs:F2} ms");
        writer.WriteLine($"- Documents Analyzed    : {docCount}");
        writer.WriteLine($"- Avg. Doc Analysis Time: {avgMs:F2} ms");
        writer.WriteLine($"- Total Violations Found: {_documentEntries.Sum(d => d.ViolationsCount)}");
    }

    private static void WritePhaseLine(StreamWriter writer, string label, double phaseMs, double totalMs)
    {
        var pct = totalMs > 0 ? (phaseMs / totalMs) * 100 : 0.0;
        writer.WriteLine($"{label,-35}: {phaseMs,10:F2} ms ({pct,5:F1}%)");
    }
}

/// <summary>
/// Repräsentiert die gemessene Leistung für ein einzelnes Dokument.
/// </summary>
public sealed record DocumentPerformanceEntry
{
    public required string FilePath { get; init; }
    public required double DurationMs { get; init; }
    public required int ViolationsCount { get; init; }
}

/// <summary>
/// Repräsentiert den strukturierten Bericht im JSON-Format.
/// </summary>
public sealed record ProfilerJsonReport
{
    public required string SolutionName { get; init; }
    public required string SolutionPath { get; init; }
    public string? RulesPath { get; init; }
    public string? Arguments { get; init; }
    public required string Timestamp { get; init; }
    public required ProfilerSummary Summary { get; init; }
    public required Dictionary<string, double> PostAnalysisSteps { get; init; }
    public required List<DocumentPerformanceEntry> Documents { get; init; }
}

/// <summary>
/// Zusammenfassende Performance-Metriken.
/// </summary>
public sealed record ProfilerSummary
{
    public required double TotalDurationMs { get; init; }
    public required double WorkspaceLoadDurationMs { get; init; }
    public required double AutoFixDurationMs { get; init; }
    public required double AnalysisDurationMs { get; init; }
    public required double PostAnalysisDurationMs { get; init; }
    public required double OptionalOutputsDurationMs { get; init; }
    public required double OutputWritingDurationMs { get; init; }
    public required int DocumentCount { get; init; }
    public required int TotalViolationsCount { get; init; }
    public required double AverageDocumentAnalysisDurationMs { get; init; }
}
