#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static partial class AssemblyAnalysisContextFactory
{
    private static async Task<ProjectCompilationResult> TryGetProjectCompilationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        try
        {
            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            if (compilation?.Assembly is not null)
            {
                return new(compilation, CreateCompilationDiagnostics(project, compilation, cancellationToken), null);
            }

            return await BuildFallbackOrErrorResultAsync(project, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException or NotSupportedException)
        {
            return await BuildFallbackOrErrorResultAsync(project, ex, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ProjectCompilationResult> BuildFallbackOrErrorResultAsync(
        Project project,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        var fallback = await TryBuildFallbackCompilationAsync(project, cancellationToken).ConfigureAwait(false);
        if (fallback?.Assembly is not null)
        {
            var diagnostics = CreateCompilationDiagnostics(project, fallback, cancellationToken).ToList();
            var message = exception is null
                ? $"Projekt-Compilation für '{project.Name}' wurde aus den Quelldokumenten erzeugt (partieller Modus)."
                : $"Workspace-Laden für '{project.Name}' schlug fehl ({exception.Message}); Quelldokumente wurden im partiellen Modus geladen.";
            var code = exception is null
                ? ExternalSourceConfigurationDiagnosticCodes.WorkspaceDiagnostic
                : ExternalSourceConfigurationDiagnosticCodes.CompilationFailed;
            diagnostics.Insert(0, new(code, message, "warning", project.FilePath ?? project.Name));
            return new(fallback, diagnostics, null);
        }

        var error = exception is null
            ? $"Source-Project-Compilation '{project.Name}' ergab kein gültiges Assembly-Symbol und keine lesbaren Quelldokumente."
            : $"Source-Project-Compilation '{project.Name}' konnte nicht geladen werden: {exception.Message}";
        return new(
            null,
            [new(
                ExternalSourceConfigurationDiagnosticCodes.CompilationFailed,
                error,
                "error",
                project.FilePath ?? project.Name)],
            error);
    }

    private static async Task<Compilation?> TryBuildFallbackCompilationAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var syntaxTrees = new List<SyntaxTree>();
        foreach (var document in project.Documents)
        {
            if (await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false) is { } tree)
            {
                syntaxTrees.Add(tree);
            }
        }

        if (syntaxTrees.Count == 0)
        {
            return null;
        }

        return CSharpCompilation.Create(
            project.AssemblyName ?? project.Name,
            syntaxTrees,
            project.MetadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static IReadOnlyList<ExternalSourceConfigurationDiagnostic> CreateCompilationDiagnostics(
        Project project,
        Compilation? compilation,
        CancellationToken cancellationToken)
    {
        if (compilation is null) return [];
        return compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity is not DiagnosticSeverity.Hidden)
            .Take(20)
            .Select(diagnostic => new ExternalSourceConfigurationDiagnostic(
                diagnostic.Id,
                diagnostic.GetMessage(),
                diagnostic.Severity.ToString().ToLowerInvariant(),
                GetCompilationDiagnosticLocation(project, diagnostic)))
            .ToArray();
    }

    private static string GetCompilationDiagnosticLocation(Project project, Diagnostic diagnostic)
    {
        var path = diagnostic.Location == Location.None
            ? null
            : diagnostic.Location.GetLineSpan().Path;
        return string.IsNullOrWhiteSpace(path) ? project.FilePath ?? project.Name : path;
    }
}
