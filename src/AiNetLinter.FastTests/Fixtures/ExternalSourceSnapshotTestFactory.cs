#nullable enable

using System.IO;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.FastTests.Fixtures;

internal sealed record ExternalSourceProjectSpec(
    string Name,
    string AssemblyName,
    string Source,
    IReadOnlyList<string>? ProjectReferences = null);

internal static class ExternalSourceSnapshotTestFactory
{
    internal static ExternalSourceSnapshot CreateSnapshot(
        string rootPath,
        ExternalSourceMapping mapping,
        params ExternalSourceProjectSpec[] projectSpecs)
        => CreateSnapshot(rootPath, mapping, "revision-1", null, projectSpecs);

    internal static ExternalSourceSnapshot CreateSnapshot(
        string rootPath,
        ExternalSourceMapping mapping,
        string loadedRevision,
        ExternalSourceCheckoutHandle? checkoutOwner,
        params ExternalSourceProjectSpec[] projectSpecs)
    {
        var workspace = new AdhocWorkspace();
        var solutionPath = Path.Combine(rootPath, "ExternalSource.slnx");
        var solution = workspace.AddSolution(SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            filePath: solutionPath));
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        var projectIds = projectSpecs.ToDictionary(spec => spec.Name, spec => ProjectId.CreateNewId(spec.Name));
        foreach (var spec in projectSpecs)
        {
            var projectId = projectIds[spec.Name];
            var projectDirectory = Path.Combine(solutionDirectory, spec.Name);
            var projectPath = Path.Combine(projectDirectory, spec.Name + ".csproj");
            var projectInfo = ProjectInfo.Create(
                    projectId,
                    VersionStamp.Create(),
                    spec.Name,
                    spec.AssemblyName,
                    LanguageNames.CSharp,
                    filePath: projectPath)
                .WithMetadataReferences(RoslynTestSolutionFactory.CoreReferences)
                .WithCompilationOptions(new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    nullableContextOptions: NullableContextOptions.Enable));
            solution = solution.AddProject(projectInfo);
            solution = solution.AddDocument(
                DocumentId.CreateNewId(projectId),
                "Source.cs",
                spec.Source,
                filePath: Path.Combine(projectDirectory, "Source.cs"));
        }

        foreach (var spec in projectSpecs)
        {
            if (spec.ProjectReferences is null) continue;
            var projectId = projectIds[spec.Name];
            foreach (var referencedName in spec.ProjectReferences)
            {
                solution = solution.AddProjectReference(
                    projectId,
                    new ProjectReference(projectIds[referencedName]));
            }
        }

        return new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, loadedRevision),
            solution,
            workspace,
            new ExternalSourceSnapshotOwnership(
                checkoutOwner,
                IsAttested: true));
    }
}
