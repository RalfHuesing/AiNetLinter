#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal sealed class AssemblyRoslynWorkspaceFactory
{
    internal async Task<AssemblyRoslynSnapshot> CreateAsync(
        AssemblyWorkspaceRequest request,
        string assemblyName,
        string contentHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Documents.Count == 0)
        {
            throw new InvalidOperationException("Der synthetische Roslyn-Snapshot benötigt mindestens ein Dokument.");
        }

        var workspace = new AdhocWorkspace();
        try
        {
            var projectId = ProjectId.CreateNewId(AssemblyCacheContract.SyntheticProjectName);
            var references = EnsureCoreLibraryReference(request);
            var projectInfo = CreateProjectInfo(projectId, assemblyName, request, references);
            var solution = workspace.AddProject(projectInfo).Solution;
            var origins = new Dictionary<DocumentId, AssemblyOrigin>();
            foreach (var document in request.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var documentId = DocumentId.CreateNewId(projectId);
                solution = solution.AddDocument(
                    documentId,
                    Path.GetFileName(document.GeneratedPath),
                    SourceText.From(document.CSharpSource, Encoding.UTF8),
                    filePath: document.GeneratedPath);
                origins[documentId] = new AssemblyOrigin(
                    "decompiled",
                    request.Fingerprint.CanonicalPath,
                    contentHash,
                    document.GeneratedPath,
                    request.Status == AssemblySessionStatus.Complete ? "high" : "medium");
            }

            if (!workspace.TryApplyChanges(solution))
            {
                throw new InvalidOperationException("Der synthetische Roslyn-Snapshot konnte nicht veröffentlicht werden.");
            }

            var project = solution.GetProject(projectId)
                ?? throw new InvalidOperationException("Das synthetische Assembly-Projekt konnte nicht erzeugt werden.");
            var projectDocuments = ValidateDocuments(project, request, workspace);

            var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Die synthetische Assembly-Compilation konnte nicht erzeugt werden.");
            return new AssemblyRoslynSnapshot(
                solution,
                projectId,
                compilation,
                projectDocuments,
                origins,
                workspace);
        }
        catch
        {
            workspace.Dispose();
            throw;
        }
    }

    private static ImmutableArray<MetadataReference> EnsureCoreLibraryReference(AssemblyWorkspaceRequest request)
    {
        var references = request.MetadataReferences
            .Where(reference => !IsTargetReference(reference, request.AssemblyPath))
            .ToImmutableArray();
        var coreLibraryPath = typeof(object).Assembly.Location;
        return references.Any(reference => reference is PortableExecutableReference portable
                && string.Equals(portable.FilePath, coreLibraryPath, StringComparison.OrdinalIgnoreCase))
            ? references
            : references.Add(MetadataReference.CreateFromFile(coreLibraryPath));
    }

    private static IReadOnlyList<Document> ValidateDocuments(
        Project project,
        AssemblyWorkspaceRequest request,
        AdhocWorkspace workspace)
    {
        var documents = project.Documents.ToList();
        if (documents.Count != request.Documents.Count
            || documents.Any(document => string.IsNullOrWhiteSpace(document.FilePath)))
        {
            workspace.Dispose();
            throw new InvalidOperationException("Der Roslyn-Snapshot enthält nicht alle erwarteten Dokumente.");
        }

        return documents;
    }

    private static ProjectInfo CreateProjectInfo(
        ProjectId projectId,
        string assemblyName,
        AssemblyWorkspaceRequest request,
        ImmutableArray<MetadataReference> references)
    {
        var generatedDocumentPath = request.Documents.FirstOrDefault()?.GeneratedPath;
        var generatedProjectDirectory = Path.GetDirectoryName(generatedDocumentPath ?? string.Empty);
        if (string.IsNullOrWhiteSpace(generatedProjectDirectory))
        {
            generatedProjectDirectory = Path.GetDirectoryName(Path.GetFullPath(request.AssemblyPath));
        }

        generatedProjectDirectory ??= AppContext.BaseDirectory;
        var generatedProjectPath = Path.Combine(generatedProjectDirectory, assemblyName + ".csproj");
        return ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                assemblyName,
                assemblyName,
                LanguageNames.CSharp,
                filePath: generatedProjectPath,
                compilationOptions: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable),
                parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
                metadataReferences: references);
    }

    private static bool IsTargetReference(MetadataReference reference, string assemblyPath) =>
        reference is PortableExecutableReference portable
        && portable.FilePath is not null
        && string.Equals(
            Path.GetFullPath(portable.FilePath),
            Path.GetFullPath(assemblyPath),
            StringComparison.OrdinalIgnoreCase);
}
