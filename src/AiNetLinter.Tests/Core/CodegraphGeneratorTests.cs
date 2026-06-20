#nullable enable

using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;
using AiNetLinter.Core;
using AiNetLinter.Generators;

namespace AiNetLinter.Tests.Core;

// @covers CodegraphGenerator
public sealed class CodegraphGeneratorTests
{
    private static Solution CreateTestSolution(params (string FileName, string Source)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "AppProject",
            "AppProject",
            LanguageNames.CSharp,
            metadataReferences: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
            });

        var solution = workspace.CurrentSolution.AddProject(projectInfo);

        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.FileName, file.Source);
        }

        return solution;
    }

    [Fact]
    public async Task GenerateAsync_ProducesCompactTextWithRelationships()
    {
        const string baseSource = @"
public interface IBaseService
{
    void Run();
}
";
        const string classASource = @"
public class BaseClass
{
    public void BaseMethod() {}
}
";
        const string classBSource = @"
public sealed class ChildClass : BaseClass, IBaseService
{
    private readonly BaseClass _dependency;
    public IBaseService ServiceProp { get; set; }

    public ChildClass(BaseClass dep, IBaseService svc)
    {
        _dependency = dep;
        ServiceProp = svc;
    }

    public void Run() {}
    public int GetValue() => 42;
}
";
        var solution = CreateTestSolution(
            ("IBaseService.cs", baseSource),
            ("BaseClass.cs", classASource),
            ("ChildClass.cs", classBSource)
        );

        var tempFile = Path.GetTempFileName();
        try
        {
            await CodegraphGenerator.GenerateAsync(solution, tempFile);

            var content = await File.ReadAllTextAsync(tempFile);

            // Alle drei Typen vorhanden
            Assert.Contains("BaseClass", content);
            Assert.Contains("ChildClass", content);
            Assert.Contains("IBaseService", content);

            // Interface-Marker
            Assert.Contains("[interface]", content);

            // Vererbung
            Assert.Contains(": BaseClass", content);

            // Interface-Implementierung
            Assert.Contains("impl IBaseService", content);

            // Abhaengigkeiten (Felder + Properties + Konstruktor-Parameter)
            Assert.Contains("→ ", content);
            Assert.Contains("BaseClass", content);

            // Kein Methoden-Listing
            Assert.DoesNotContain("+Run()", content);
            Assert.DoesNotContain("+BaseMethod()", content);
            Assert.DoesNotContain("+GetValue()", content);

            // Kein Mermaid
            Assert.DoesNotContain("```mermaid", content);
            Assert.DoesNotContain("classDiagram", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateAsync_GroupsByNamespace()
    {
        const string source = @"
namespace App.Services { public sealed class UserService {} }
namespace App.Models { public sealed record User(string Name); }
";
        var solution = CreateTestSolution(("File.cs", source));

        var tempFile = Path.GetTempFileName();
        try
        {
            await CodegraphGenerator.GenerateAsync(solution, tempFile);
            var content = await File.ReadAllTextAsync(tempFile);

            Assert.Contains("## App.Services", content);
            Assert.Contains("## App.Models", content);
            Assert.Contains("[record]", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GenerateAsync_PartialClass_ShowsPartialModifier()
    {
        const string part1 = "public partial class MyService { public void MethodA() {} }";
        const string part2 = "public partial class MyService { public void MethodB() {} }";
        var solution = CreateTestSolution(("Part1.cs", part1), ("Part2.cs", part2));

        var tempFile = Path.GetTempFileName();
        try
        {
            await CodegraphGenerator.GenerateAsync(solution, tempFile);
            var content = await File.ReadAllTextAsync(tempFile);

            Assert.Contains("partial", content);
            Assert.DoesNotContain("+MethodA()", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
