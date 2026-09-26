#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

/// <summary>Begrenzte Markupauswertung pro Snapshot einschließlich physischer Projektdateien.</summary>
internal sealed class DeadCodeMarkupUsage(DeadCodeUsageIndex index)
{
    private const int MaximumFiles = 2000;
    private const int MaximumFileBytes = 1024 * 1024;
    private readonly HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);

    internal async Task CollectAsync(Solution solution, CancellationToken ct, Config? config = null)
    {
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync(ct);
            if (compilation is null) continue;
            var role = DeadCodeProjectRole.Resolve(project, config);
            foreach (var document in project.AdditionalDocuments.Concat<TextDocument>(project.Documents).Where(document => IsMarkup(document.Name)))
            {
                var text = await document.GetTextAsync(ct);
                Collect(new(compilation, role, document.FilePath ?? document.Name, text.ToString(), project));
            }
            await ReadProjectFilesAsync(new(project, compilation, role), ct);
        }
    }

    private async Task ReadProjectFilesAsync(MarkupProject context, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(context.Project.FilePath);
        if (directory is null || !Directory.Exists(directory)) return;
        try
        {
            foreach (var path in EnumerateFiles(directory, ct))
            {
                ct.ThrowIfCancellationRequested();
                if (!IsMarkup(path) || visited.Contains(context.Compilation.AssemblyName + ":" + path)) continue;
                if (visited.Count >= MaximumFiles || new FileInfo(path).Length > MaximumFileBytes)
                {
                    index.CoverageGaps.Add("markup_limit");
                    return;
                }
                var text = await File.ReadAllTextAsync(path, ct);
                Collect(new(context.Compilation, context.Role, path, text, context.Project));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            index.CoverageGaps.Add("markup_unreadable");
        }
    }

    private void Collect(DeadCodeMarkupDocument source)
    {
        if (!visited.Add(source.Compilation.AssemblyName + ":" + source.Path)) return;
        if (visited.Count > MaximumFiles || source.Text.Length > MaximumFileBytes)
        {
            index.CoverageGaps.Add("markup_limit");
            return;
        }
        if (source.Path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)) new DeadCodeXamlUsage(index).Collect(source);
        else new DeadCodeRazorUsage(index).Collect(source);
    }

    private static bool IsMarkup(string path) => Path.GetExtension(path).ToLowerInvariant() is ".xaml" or ".razor" or ".js";

    private static IEnumerable<string> EnumerateFiles(string directory, CancellationToken ct)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.TryPop(out var current))
        {
            ct.ThrowIfCancellationRequested();
            if (current != directory && Directory.EnumerateFiles(current, "*.csproj").Any()) continue;
            foreach (var file in Directory.EnumerateFiles(current)) yield return file;
            foreach (var child in Directory.EnumerateDirectories(current))
            {
                if (Path.GetFileName(child) is "bin" or "obj" or ".git" or "node_modules" or ".codex") continue;
                if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) continue;
                pending.Push(child);
            }
        }
    }

    private sealed record MarkupProject(Project Project, Compilation Compilation, string Role);
}

internal sealed record DeadCodeMarkupDocument(Compilation Compilation, string Role, string Path, string Text, Project Project);
