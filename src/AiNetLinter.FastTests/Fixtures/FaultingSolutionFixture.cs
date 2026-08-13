#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.FastTests.Fixtures;

internal sealed class FaultingSolutionFixture : IDisposable
{
    private readonly AdhocWorkspace workspace = new();

    public FaultingSolutionFixture()
    {
        var projectId = ProjectId.CreateNewId("FaultyProject");
        var project = ProjectInfo.Create(projectId, VersionStamp.Create(), "FaultyProject", "FaultyProject", LanguageNames.CSharp)
            .WithMetadataReferences(RoslynTestSolutionFactory.CoreReferences);
        var solution = workspace.AddSolution(SolutionInfo.Create(
                SolutionId.CreateNewId(),
                VersionStamp.Create(),
                filePath: @"C:\ainetlinter-virtual\Faulty.slnx"))
            .AddProject(project);
        var document = DocumentInfo.Create(
            DocumentId.CreateNewId(projectId),
            "Faulty.cs",
            loader: new ThrowingTextLoader(),
            filePath: @"C:\ainetlinter-virtual\FaultyProject\Faulty.cs");
        Solution = solution.AddDocument(document);
    }

    public Solution Solution { get; }

    public void Dispose() => workspace.Dispose();

    private sealed class ThrowingTextLoader : TextLoader
    {
        public override Task<TextAndVersion> LoadTextAndVersionAsync(LoadTextOptions options, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Simulierter Lesefehler fuer Malfunction-Regressionstest.");
    }
}
