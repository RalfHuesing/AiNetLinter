#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.FastTests.Core;

/// <summary>
/// MSE-Baustein "vorbereitete Solution analysieren, regelkonformes Ergebnis und deterministischer
/// Fehlerweg": ruft <see cref="LinterEngine.RunAsync(Solution, bool, int, System.Threading.CancellationToken)"/>
/// direkt gegen eine per <see cref="AdhocWorkspace"/> aufgebaute Zwei-Klassen-Solution auf (kein
/// MSBuild, keine Platte) und prueft sowohl den Verletzungs- als auch den regelkonformen Pfad
/// deterministisch. Nutzt den internal LinterEngine-Konstruktor ueber
/// InternalsVisibleTo("AiNetLinter.FastTests").
/// </summary>
[Trait("Category", "Component")]
public sealed class LinterEngineSolutionAnalysisTests
{
    private static Config CreateConfig() => new()
    {
        Global = new GlobalConfig { EnforceSealedClasses = true },
        Metrics = new MetricsConfig(),
    };

    private static Solution CreateAdhocSolution(params (string FileName, string Content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
                projectId,
                VersionStamp.Create(),
                "SolutionAnalysisTestProject",
                "SolutionAnalysisTestProject",
                LanguageNames.CSharp)
            .WithMetadataReferences([mscorlib])
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Content);
        }

        return solution;
    }

    [Fact]
    public async Task RunAsync_PreparedSolutionWithSealedClassViolation_FlagsViolatingClassAndSparesCompliantClass()
    {
        const string violatingClass = """
            namespace SolutionAnalysis;
            public class UnsealedService
            {
                public void Do() {}
            }
            """;
        const string compliantClass = """
            namespace SolutionAnalysis;
            public sealed class SealedService
            {
                public void Do() {}
            }
            """;

        var solution = CreateAdhocSolution(
            ("UnsealedService.cs", violatingClass),
            ("SealedService.cs", compliantClass));

        var engine = new LinterEngine(CreateConfig());
        var violations = await engine.RunAsync(solution);

        Assert.Contains(violations, v =>
            v.RuleName == nameof(GlobalConfig.EnforceSealedClasses) &&
            v.FilePath.EndsWith("UnsealedService.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(violations, v => v.FilePath.EndsWith("SealedService.cs", StringComparison.Ordinal));
    }
}
