#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal static class DeadCodeChangeScope
{
    internal static async Task ExpandAsync(DeadCodeScanContext context, CancellationToken ct)
    {
        if (context.Args.ScopeFiles is not { } scope) return;
        var previous = context.Args.PreviousSolution ?? await ReadPreviousAsync(context.Solution, scope, ct);
        context.Progress.ChangesBasis = previous is null ? "unavailable" : "available";
        if (previous is null) return;
        var expanded = new HashSet<string>(scope, StringComparer.OrdinalIgnoreCase);
        foreach (var document in previous.Projects.SelectMany(project => project.Documents)
            .Where(document => document.FilePath is not null && scope.Contains(document.FilePath)))
        {
            var model = await document.GetSemanticModelAsync(ct);
            var root = await document.GetSyntaxRootAsync(ct);
            if (model is null || root is null) continue;
            CollectTargets(model, root, expanded, context.Progress.PriorTargets, ct);
        }
        if (scope.Any(path => Path.GetExtension(path) is ".razor" or ".xaml" or ".js" or ".json"))
            foreach (var path in context.Solution.Projects.SelectMany(project => project.Documents).Select(document => document.FilePath).OfType<string>())
                expanded.Add(path);
        context.Args = context.Args with { ScopeFiles = expanded };
    }

    private static void CollectTargets(SemanticModel model, SyntaxNode root, HashSet<string> expanded,
        HashSet<string> targets, CancellationToken ct)
    {
        foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
        {
            ct.ThrowIfCancellationRequested();
            var symbol = model.GetSymbolInfo(name, ct).Symbol;
            if (symbol is IMethodSymbol { ReducedFrom: { } reduced }) symbol = reduced;
            if (symbol is null) continue;
            targets.Add(DeadCodeUsageIndex.Key(symbol));
            foreach (var reference in symbol.DeclaringSyntaxReferences) expanded.Add(reference.SyntaxTree.FilePath);
        }
    }

    private static async Task<Solution?> ReadPreviousAsync(Solution solution, IReadOnlySet<string> scope, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(solution.FilePath);
        if (directory is null || !Directory.Exists(directory)) return null;
        var head = await ReadGitAsync(directory, "rev-parse", "--verify", "HEAD", ct);
        if (head is null) return null;
        foreach (var document in solution.Projects.SelectMany(project => project.Documents)
            .Where(document => document.FilePath is not null && scope.Contains(document.FilePath)))
        {
            var path = Path.GetRelativePath(directory, document.FilePath!).Replace('\\', '/');
            var text = await ReadGitAsync(directory, "show", "HEAD:./" + path, null, ct);
            solution = text is null ? solution.RemoveDocument(document.Id)
                : solution.WithDocumentText(document.Id, SourceText.From(text));
        }
        return solution;
    }

    private static async Task<string?> ReadGitAsync(string directory, string command, string argument, string? last, CancellationToken ct)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true,
            UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add(command);
        start.ArgumentList.Add(argument);
        if (last is not null) start.ArgumentList.Add(last);
        using var process = Process.Start(start);
        if (process is null) return null;
        using var registration = ct.Register(() => { if (!process.HasExited) process.Kill(entireProcessTree: true); });
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var error = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await error;
        return process.ExitCode == 0 ? await output : null;
    }
}
