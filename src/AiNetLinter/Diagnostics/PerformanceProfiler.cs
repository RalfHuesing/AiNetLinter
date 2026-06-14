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
public sealed class PerformanceProfiler
{
    private static readonly Lazy<PerformanceProfiler> LazyInstance = new(() => new PerformanceProfiler());
    
    /// <summary>
    /// Singleton-Instanz des Profilers.
    /// </summary>
    public static PerformanceProfiler Instance => LazyInstance.Value;

    private bool _initialized;
    private bool _enabled = true;
    
    private readonly Stopwatch _totalStopwatch = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _phaseStopwatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, double> _phaseDurations = new(StringComparer.OrdinalIgnoreCase);
    
    private readonly ConcurrentBag<DocumentPerformanceEntry> _documentEntries = new();
    private readonly ConcurrentDictionary<string, double> _postAnalysisStepDurations = new(StringComparer.OrdinalIgnoreCase);

    private PerformanceProfiler()
    {
    }

    /// <summary>
    /// Gibt an, ob Profiling aktiviert ist.
    /// </summary>
    public bool IsEnabled => _enabled;

    /// <summary>
    /// Initialisiert den Profiler. Startet die Gesamtlaufzeit-Messung, falls aktiviert.
    /// </summary>
    public void Initialize(bool enabled)
    {
        if (_initialized) return;
        _enabled = enabled;
        _initialized = true;
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

    /// <summary>
    /// Generiert die Berichte im base-Verzeichnis unter measurements/ (wenn aktiviert).
    /// </summary>
    public void WriteReport(string targetPath, string? solutionFilePath)
    {
        if (!_enabled || !_initialized) return;
        _totalStopwatch.Stop();

        try
        {
            var solutionName = "UnknownProject";
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                solutionName = Path.GetFileNameWithoutExtension(solutionFilePath);
            }
            else if (!string.IsNullOrEmpty(targetPath))
            {
                var cleanedPath = targetPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                solutionName = Path.GetFileName(cleanedPath);
                if (string.IsNullOrEmpty(solutionName))
                {
                    solutionName = "TargetProject";
                }
            }

            var timestamp = DateTime.Now;
            var dirName = $"{solutionName}-{timestamp:yyyy-MM-dd-HH-mm-ss-fff}-{Guid.NewGuid().ToString("N")[..8]}";
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var measurementsDir = Path.Combine(baseDir, "measurements");
            var targetDir = Path.Combine(measurementsDir, dirName);

            Directory.CreateDirectory(targetDir);

            var totalMs = _totalStopwatch.Elapsed.TotalMilliseconds;
            _phaseDurations.TryGetValue("WorkspaceLoading", out var workspaceLoadMs);
            _phaseDurations.TryGetValue("AutoFix", out var autoFixMs);
            _phaseDurations.TryGetValue("DocumentAnalysis", out var documentAnalysisMs);
            _phaseDurations.TryGetValue("PostAnalysis", out var postAnalysisMs);
            _phaseDurations.TryGetValue("OptionalOutputs", out var optionalOutputsMs);
            _phaseDurations.TryGetValue("OutputWriting", out var outputWritingMs);

            var documentCount = _documentEntries.Count;
            var totalViolations = _documentEntries.Sum(d => d.ViolationsCount);
            var avgDocMs = documentCount > 0 ? _documentEntries.Average(d => d.DurationMs) : 0.0;

            // 1. JSON Report schreiben
            var jsonReport = new ProfilerJsonReport
            {
                SolutionName = solutionName,
                SolutionPath = solutionFilePath ?? targetPath,
                Timestamp = timestamp.ToString("o"),
                Summary = new ProfilerSummary
                {
                    TotalDurationMs = totalMs,
                    WorkspaceLoadDurationMs = workspaceLoadMs,
                    AutoFixDurationMs = autoFixMs,
                    AnalysisDurationMs = documentAnalysisMs,
                    PostAnalysisDurationMs = postAnalysisMs,
                    OptionalOutputsDurationMs = optionalOutputsMs,
                    OutputWritingDurationMs = outputWritingMs,
                    DocumentCount = documentCount,
                    TotalViolationsCount = totalViolations,
                    AverageDocumentAnalysisDurationMs = avgDocMs
                },
                PostAnalysisSteps = _postAnalysisStepDurations.ToDictionary(k => k.Key, v => v.Value),
                Documents = _documentEntries.OrderByDescending(d => d.DurationMs).ToList()
            };

            var jsonPath = Path.Combine(targetDir, "performance.json");
            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(jsonPath, JsonSerializer.Serialize(jsonReport, jsonOptions), Encoding.UTF8);

            // 2. Human-readable Text Log schreiben
            var logPath = Path.Combine(targetDir, "performance.log");
            using var writer = new StreamWriter(logPath, false, Encoding.UTF8);
            
            writer.WriteLine($"=== PERFORMANCE LOG: {solutionName} ===");
            writer.WriteLine($"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff}");
            writer.WriteLine($"Target Path: {targetPath}");
            if (!string.IsNullOrEmpty(solutionFilePath))
            {
                writer.WriteLine($"Solution File: {solutionFilePath}");
            }
            writer.WriteLine();

            writer.WriteLine("--- Phases ---");
            WritePhaseLine(writer, "Workspace Loading", workspaceLoadMs, totalMs);
            WritePhaseLine(writer, "Auto-Fix Execution", autoFixMs, totalMs);
            WritePhaseLine(writer, "Document Analysis (overall)", documentAnalysisMs, totalMs);
            WritePhaseLine(writer, "Post-Analysis Checks (overall)", postAnalysisMs, totalMs);
            WritePhaseLine(writer, "Optional Outputs (Playbook/Graph)", optionalOutputsMs, totalMs);
            WritePhaseLine(writer, "Output Writing", outputWritingMs, totalMs);
            writer.WriteLine();

            if (_postAnalysisStepDurations.Count > 0)
            {
                writer.WriteLine("--- Post-Analysis Steps ---");
                foreach (var step in _postAnalysisStepDurations.OrderByDescending(s => s.Value))
                {
                    writer.WriteLine($"  {step.Key,-30}: {step.Value,10:F2} ms");
                }
                writer.WriteLine();
            }

            if (_documentEntries.Count > 0)
            {
                writer.WriteLine("--- Top 20 Slowest Documents ---");
                var topSlowDocs = _documentEntries.OrderByDescending(d => d.DurationMs).Take(20);
                foreach (var doc in topSlowDocs)
                {
                    writer.WriteLine($"  {doc.DurationMs,10:F2} ms | Violations: {doc.ViolationsCount,3} | {doc.FilePath}");
                }
                writer.WriteLine();
            }

            writer.WriteLine("=== SUMMARY ===");
            writer.WriteLine($"- Total Run Duration    : {totalMs:F2} ms");
            writer.WriteLine($"- Documents Analyzed    : {documentCount}");
            writer.WriteLine($"- Avg. Doc Analysis Time: {avgDocMs:F2} ms");
            writer.WriteLine($"- Total Violations Found: {totalViolations}");

            Console.WriteLine($"[INFO]: Performance-Messdaten erzeugt unter: {targetDir}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR]: Fehler beim Schreiben des Performance-Reports: {ex.Message}");
        }
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
