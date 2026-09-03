#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.CSharp.ProjectDecompiler;
using ICSharpCode.Decompiler.Metadata;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyDecompilationAdapter
{
    internal AssemblyBodyResolver CreateBodyResolver(
        string assemblyPath,
        AssemblyReferenceResolution references,
        AssemblyDecompilationOptions options) =>
        AssemblyDecompiledBodyResolver.Create(
            assemblyPath, references, options);

    internal Task<DecompilationResult> DecompileAsync(
        DecompilationRequest request,
        AssemblyReferenceResolution references)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        deadline.CancelAfter(request.Options.EffectiveTimeout);
        var ownsStagingDirectory = request.StagingDirectory is null;
        var stagingDirectory = request.StagingDirectory ?? Path.Combine(
            Path.GetTempPath(),
            "ainetlinter-decompilation-" + Guid.NewGuid().ToString("N"));
        var diagnostics = new List<AssemblySessionDiagnostic>();

        try
        {
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
                AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompilationRequest)),
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
        _ = references;
        using var module = new PEFile(request.AssemblyPath);
        var targetFrameworkId = module.DetectTargetFrameworkId();
        var resolver = new UniversalAssemblyResolver(request.AssemblyPath, throwOnError: false, targetFrameworkId);
        var assemblyDirectory = Path.GetDirectoryName(request.AssemblyPath);
        if (!string.IsNullOrWhiteSpace(assemblyDirectory)) resolver.AddSearchDirectory(assemblyDirectory);
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
                        AssemblyDiagnosticSeverity.Error));
                    continue;
                }

                var syntaxErrors = CSharpSyntaxTree.ParseText(source)
                    .GetDiagnostics(cancellationToken)
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Take(3)
                    .ToList();
                if (syntaxErrors.Count > 0)
                {
                    diagnostics.Add(new AssemblySessionDiagnostic(
                        AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)),
                        $"Die dekompilierte Datei '{Path.GetFileName(path)}' ist nicht parsbar: {string.Join("; ", syntaxErrors.Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}.",
                        AssemblyDiagnosticSeverity.Error));
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
                    AssemblyDiagnosticSeverity.Error));
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

    internal static CSharpDecompiler CreateDecompiler(
        string assemblyPath,
        AssemblyReferenceResolution references,
        CancellationToken cancellationToken,
        bool decompileMemberBodies = false)
    {
        var settings = new DecompilerSettings
        {
            DecompileMemberBodies = decompileMemberBodies,
            ShowXmlDocumentation = false,
            UseDebugSymbols = false,
            RequiredMembers = false,
            AsyncAwait = true,
            AsyncEnumerator = true,
            AnonymousMethods = true,
            AnonymousTypes = true,
            LocalFunctions = true,
            YieldReturn = true,
        };
        return new CSharpDecompiler(assemblyPath, references.DecompilerResolver, settings)
        {
            CancellationToken = cancellationToken,
        };
    }
}
