#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;
using AiNetLinter.Tests.Output;

namespace AiNetLinter.Tests.Core;

/// <summary>
/// End-to-End-Absicherung des Restore-Erkennungsmechanismus (siehe rationale.md): ein Projekt
/// ohne frisches <c>obj/project.assets.json</c> darf keine massenhaften Phantom-Violations pro
/// unaufloesbarem using erzeugen, sondern genau EINE klare <c>PROJECT_NOT_RESTORED</c>-Diagnose.
/// Ein sauber restoretes Projekt mit einem echten unaufloesbaren using muss weiterhin melden.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LinterEngineProjectRestoreTests : IDisposable
{
    private readonly string _tempDir;

    public LinterEngineProjectRestoreTests()
    {
        _tempDir = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"ainetlinter-restore-engine-{Guid.NewGuid():N}")).FullName;
    }

    public void Dispose() => TestHelper.TryDeleteDirectoryRecursive(_tempDir);

    private static Config CreateConfigWithPhantomCheck()
    {
        return TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig { DetectAndBanPhantomDependencies = true }
        };
    }

    private const string SourceWithUnresolvableUsing = @"
using TotallyMissing.PhantomPackage;

namespace SampleNs;
public sealed class Foo
{
}";

    private Solution CreateSolutionWithProjectFile(bool restored)
    {
        var projectFile = Path.Combine(_tempDir, "Sample.csproj");
        File.WriteAllText(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        if (restored)
        {
            var objDir = Path.Combine(_tempDir, "obj");
            Directory.CreateDirectory(objDir);
            var assetsPath = Path.Combine(objDir, "project.assets.json");
            File.WriteAllText(assetsPath, "{}");
            File.SetLastWriteTimeUtc(assetsPath, File.GetLastWriteTimeUtc(projectFile).AddSeconds(5));
        }

        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), "Sample", "Sample", LanguageNames.CSharp)
            .WithFilePath(projectFile)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        var documentId = DocumentId.CreateNewId(projectId);
        solution = solution.AddDocument(documentId, "Foo.cs", SourceWithUnresolvableUsing);
        return solution;
    }

    [Fact]
    public async Task Run_SuppressesPhantomViolation_AndReportsSingleDiagnostic_WhenProjectNotRestored()
    {
        var solution = CreateSolutionWithProjectFile(restored: false);
        var console = new TestLintConsole();
        var engine = new LinterEngine(CreateConfigWithPhantomCheck(), console: console);

        var violations = await engine.RunAsync(solution, noCache: true);

        Assert.DoesNotContain(violations, v => v.RuleName == "DetectAndBanPhantomDependencies");
        Assert.Contains(console.Errors, e => e.Contains(LinterErrorCodes.ProjectNotRestored));
    }

    [Fact]
    public async Task Run_ReportsPhantomViolation_WhenProjectRestoredButUsingUnresolvable()
    {
        var solution = CreateSolutionWithProjectFile(restored: true);
        var console = new TestLintConsole();
        var engine = new LinterEngine(CreateConfigWithPhantomCheck(), console: console);

        var violations = await engine.RunAsync(solution, noCache: true);

        Assert.Contains(violations, v => v.RuleName == "DetectAndBanPhantomDependencies");
        Assert.DoesNotContain(console.Errors, e => e.Contains(LinterErrorCodes.ProjectNotRestored));
    }
}
