#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools;

internal static class McpCompileDiagnostics
{
    /// <summary>
    /// Liefert alle Roslyn-Compile-Fehler (<see cref="DiagnosticSeverity.Error"/>) der uebergebenen
    /// Solution, gruppiert nach Document-FilePath. Keys sind absolute Pfade aus Roslyns
    /// <c>SyntaxTree.FilePath</c> (Groß-/Kleinschreibung ignoriert). Pro Project wird die
    /// Compilation einmal via <c>Project.GetCompilationAsync</c> aufgeloest, weil jeder Aufruf
    /// potentiell den vollen Compile-Zyklus anstoesst.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<Diagnostic>>> GetErrorsByFileAsync(
        Solution solution,
        CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<Diagnostic>>(StringComparer.OrdinalIgnoreCase);

        foreach (var project in solution.Projects)
        {
            if (!project.SupportsCompilation) continue;

            var diagnostics = await GetProjectErrorsAsync(project, ct);
            foreach (var entry in diagnostics)
            {
                Accumulate(result, entry.Key, entry.Value);
            }
        }

        return result;
    }

    private static async Task<IReadOnlyList<KeyValuePair<string, Diagnostic>>> GetProjectErrorsAsync(
        Project project,
        CancellationToken ct)
    {
        var compilation = await project.GetCompilationAsync(ct);
        if (compilation is null) return [];

        var bucket = new List<KeyValuePair<string, Diagnostic>>();
        foreach (var diagnostic in compilation.GetDiagnostics(ct))
        {
            if (diagnostic.Severity != DiagnosticSeverity.Error) continue;
            if (diagnostic.Location.SourceTree?.FilePath is not { } path) continue;
            bucket.Add(new KeyValuePair<string, Diagnostic>(path, diagnostic));
        }

        return bucket;
    }

    private static void Accumulate(
        Dictionary<string, IReadOnlyList<Diagnostic>> result,
        string path,
        Diagnostic diagnostic)
    {
        if (!result.TryGetValue(path, out var list))
        {
            list = new List<Diagnostic>();
            result[path] = list;
        }

        ((List<Diagnostic>)list).Add(diagnostic);
    }

    internal static string FormatFileWarning(IReadOnlyList<Diagnostic> diagnostics, int maxShown = 3)
    {
        if (diagnostics.Count == 0) return string.Empty;

        var shown = diagnostics.Take(maxShown).Select(FormatDiagnostic);
        var suffix = diagnostics.Count > maxShown ? $" (+{diagnostics.Count - maxShown} weitere)" : string.Empty;

        return $"Hinweis: Diese Datei hat {diagnostics.Count} Compile-Fehler — Ergebnis ist moeglicherweise unvollstaendig. " +
               $"Diagnostics: {string.Join("; ", shown)}{suffix}";
    }

    internal static string FormatAggregateWarning(int fileCount, int totalErrors)
    {
        if (fileCount == 0 || totalErrors == 0) return string.Empty;

        var fileLabel = fileCount == 1 ? "Datei" : "Dateien";
        return $"Hinweis: {fileCount} {fileLabel} haben Compile-Fehler " +
               $"({totalErrors} Errors gesamt) — Details siehe get_file_skeleton fuer die betroffenen Dateien.";
    }

    private static string FormatDiagnostic(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        if (message.Length > 80)
        {
            message = message[..77] + "…";
        }

        return $"{diagnostic.Id}: {message}";
    }
}
