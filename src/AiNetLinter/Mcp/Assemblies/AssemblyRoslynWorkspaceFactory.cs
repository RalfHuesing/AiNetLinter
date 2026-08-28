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

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyRoslynWorkspaceFactory
{
    internal async Task<AssemblyRoslynSnapshot> CreateAsync(
        AssemblyWorkspaceRequest request,
        string assemblyName,
        string contentHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("decompiled-assembly");
        var references = request.MetadataReferences
            .Where(reference => !IsTargetReference(reference, request.AssemblyPath))
            .ToImmutableArray();
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
            workspace.Dispose();
            throw new InvalidOperationException("Der synthetische Roslyn-Snapshot konnte nicht veröffentlicht werden.");
        }

        var project = solution.GetProject(projectId)
            ?? throw new InvalidOperationException("Das synthetische Assembly-Projekt konnte nicht erzeugt werden.");
        var compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Die synthetische Assembly-Compilation konnte nicht erzeugt werden.");
        return new AssemblyRoslynSnapshot(
            solution,
            projectId,
            compilation,
            project.Documents.ToList(),
            origins,
            workspace);
    }

    private static ProjectInfo CreateProjectInfo(
        ProjectId projectId,
        string assemblyName,
        AssemblyWorkspaceRequest request,
        ImmutableArray<MetadataReference> references)
    {
        var generatedProjectPath = Path.Combine(
            Path.GetDirectoryName(request.Documents.FirstOrDefault()?.GeneratedPath ?? request.AssemblyPath)!,
            assemblyName + ".csproj");
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
