#nullable enable

using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.FastTests.Fixtures;

internal sealed record ExternalSourceProjectSpec(string Name, string AssemblyName, string Source);

internal static class ExternalSourceSnapshotTestFactory
{
    internal static ExternalSourceSnapshot CreateSnapshot(
        string rootPath,
        ExternalSourceMapping mapping,
        params ExternalSourceProjectSpec[] projectSpecs)
    {
        var workspace = new AdhocWorkspace();
        var solutionPath = Path.Combine(rootPath, "ExternalSource.slnx");
        var solution = workspace.AddSolution(SolutionInfo.Create(
            SolutionId.CreateNewId(),
            VersionStamp.Create(),
            filePath: solutionPath));
        var solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        foreach (var spec in projectSpecs)
        {
            var projectId = ProjectId.CreateNewId(spec.Name);
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

        return new ExternalSourceSnapshot(
            SourceSnapshotIdentity.Create(mapping, "revision-1"),
            solution,
            workspace);
    }
}
