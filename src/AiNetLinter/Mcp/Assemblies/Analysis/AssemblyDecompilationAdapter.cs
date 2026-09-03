#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyDecompilationAdapter
{
    private readonly Func<DecompilationRequest, AssemblyReferenceResolution, Task<DecompilationResult>>? decompileOverride;

    internal AssemblyDecompilationAdapter(
        Func<DecompilationRequest, AssemblyReferenceResolution, Task<DecompilationResult>>? decompileOverride = null)
    {
        this.decompileOverride = decompileOverride;
    }

    internal Task<DecompilationResult> DecompileAsync(
        DecompilationRequest request,
        AssemblyReferenceResolution references)
    {
        if (decompileOverride is not null)
        {
            return decompileOverride(request, references);
        }

        request.CancellationToken.ThrowIfCancellationRequested();
        var ownsStagingDirectory = request.StagingDirectory is null;
        var stagingDirectory = request.StagingDirectory ?? Path.Combine(
            Path.GetTempPath(),
            "ainetlinter-decompilation-" + Guid.NewGuid().ToString("N"));
        var diagnostics = new List<AssemblySessionDiagnostic>();
        if (!AssemblyDecompilationOptions.IsSupportedTimeout(request.Options.EffectiveTimeout))
        {
            diagnostics.Add(new AssemblySessionDiagnostic(
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions)),
                "Das Decompilation-Timeout liegt außerhalb des von CancellationTokenSource.CancelAfter unterstützten Bereichs.",
                AssemblyDiagnosticSeverity.Error));
            return Task.FromResult(new DecompilationResult([], diagnostics, false));
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);

        try
        {
            deadline.CancelAfter(request.Options.EffectiveTimeout);
            return Task.FromResult(DecompileProject(request, references, stagingDirectory, deadline.Token, diagnostics));
        }
        catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            diagnostics.Add(new AssemblySessionDiagnostic(
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(OperationCanceledException)),
                "Die Volldekompilierung wurde wegen des konfigurierten Timeouts abgebrochen.",
                AssemblyDiagnosticSeverity.Error));
            return Task.FromResult(ReadProjectOutput(stagingDirectory, diagnostics, CancellationToken.None, false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException or InvalidDataException or DecompilerException)
        {
            diagnostics.Add(new AssemblySessionDiagnostic(
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions)),
                $"Volldekompilierung fehlgeschlagen: {ex.Message}",
                AssemblyDiagnosticSeverity.Error));
            return Task.FromResult(ReadProjectOutput(stagingDirectory, diagnostics, CancellationToken.None, false));
        }
        finally
        {
            if (ownsStagingDirectory) AssemblyCacheCleanup.DeleteDirectory(stagingDirectory);
        }
    }

    private static DecompilationResult DecompileProject(
        DecompilationRequest request,
        AssemblyReferenceResolution references,
        string stagingDirectory,
        CancellationToken cancellationToken,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        Directory.CreateDirectory(stagingDirectory);
        using var module = new PEFile(request.AssemblyPath);
        var targetFrameworkId = module.DetectTargetFrameworkId();
        var resolver = new UniversalAssemblyResolver(request.AssemblyPath, throwOnError: false, targetFrameworkId);
        var assemblyDirectory = Path.GetDirectoryName(request.AssemblyPath);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory)) resolver.AddSearchDirectory(assemblyDirectory);
        if (references?.References is { Count: > 0 } resolvedReferences)
        {
            var extraDirectories = resolvedReferences
                .Where(reference => reference.Resolved && !string.IsNullOrWhiteSpace(reference.ResolvedPath))
                .Select(reference => Path.GetDirectoryName(reference.ResolvedPath))
                .Where(dir => !string.IsNullOrWhiteSpace(dir))
                .Select(dir => dir!)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var extraDirectory in extraDirectories)
            {
                resolver.AddSearchDirectory(extraDirectory);
            }
        }
        var decompiler = new WholeProjectDecompiler(
            new DecompilerSettings
            {
                RemoveDeadCode = true,
                YieldReturn = true,
                AsyncAwait = true,
            },
            resolver,
            projectWriter: null,
            assemblyReferenceClassifier: null,
            debugInfoProvider: null);
        decompiler.DecompileProject(module, stagingDirectory, cancellationToken);
        return ReadProjectOutput(stagingDirectory, diagnostics, cancellationToken, true);
    }

    private static DecompilationResult ReadProjectOutput(
        string stagingDirectory,
        ICollection<AssemblySessionDiagnostic> diagnostics,
        CancellationToken cancellationToken,
        bool initiallyComplete)
    {
        var projectFilePath = FindProjectFile(stagingDirectory);
        if (projectFilePath is null)
        {
            diagnostics.Add(new AssemblySessionDiagnostic(
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompilationResult.ProjectFilePath)),
                "Die Volldekompilierung hat keine .csproj-Datei materialisiert.",
                AssemblyDiagnosticSeverity.Error));
            return new DecompilationResult([], diagnostics.ToList(), false);
        }

        var documents = new List<DecompiledDocument>();
        foreach (var path in Directory.EnumerateFiles(stagingDirectory, "*.cs", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (string.IsNullOrWhiteSpace(source))
                {
                    diagnostics.Add(new AssemblySessionDiagnostic(
                        AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)),
                        $"Die dekompilierte Datei '{Path.GetFileName(path)}' ist leer.",
                        AssemblyDiagnosticSeverity.Warning));
                }
                else
                {
                    var syntaxErrors = CSharpSyntaxTree.ParseText(source)
                        .GetDiagnostics(cancellationToken)
                        .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                        .Take(3)
                        .ToList();
                    if (syntaxErrors.Count > 0)
                    {
                        diagnostics.Add(new AssemblySessionDiagnostic(
                            AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)),
                            $"Die dekompilierte Datei '{Path.GetFileName(path)}' enthält Syntaxfehler: {string.Join("; ", syntaxErrors.Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}.",
                            AssemblyDiagnosticSeverity.Warning));
                    }
                }

                documents.Add(new DecompiledDocument(
                    path,
                    Path.GetFileNameWithoutExtension(path),
                    source));
            }
            catch (IOException ex)
            {
                diagnostics.Add(new AssemblySessionDiagnostic(
                    AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.GeneratedPath)),
                    $"Die dekompilierte Datei '{Path.GetFileName(path)}' konnte nicht gelesen werden: {ex.Message}",
                    AssemblyDiagnosticSeverity.Warning));
            }
        }

        var hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == AssemblyDiagnosticSeverity.Error);
        return new DecompilationResult(documents, diagnostics.ToList(), initiallyComplete && !hasErrors, projectFilePath);
    }

    private static string? FindProjectFile(string stagingDirectory) =>
        Directory.Exists(stagingDirectory)
            ? Directory.EnumerateFiles(stagingDirectory, "*.csproj", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()
            : null;
}
