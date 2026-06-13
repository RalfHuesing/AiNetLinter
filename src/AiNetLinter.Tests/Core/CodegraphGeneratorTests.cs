#nullable enable

using System.IO;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

/// <summary>
/// Unit-Tests fuer den CodegraphGenerator.
/// </summary>
public sealed class CodegraphGeneratorTests
{
    private static Solution CreateTestSolution(params (string FileName, string Source)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
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
    public async Task GenerateAsync_CorrectlyExtractsRelationshipsAndMethods()
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

            // Verifiziere das Vorhandensein der Klassen
            Assert.Contains("class IBaseService", content);
            Assert.Contains("class BaseClass", content);
            Assert.Contains("class ChildClass", content);

            // Verifiziere public Methoden
            Assert.Contains("+Run()", content);
            Assert.Contains("+BaseMethod()", content);
            Assert.Contains("+GetValue()", content);

            // Verifiziere Generalisierung und Implementierung
            Assert.Contains("BaseClass <|-- ChildClass : erbt", content);
            Assert.Contains("IBaseService <|.. ChildClass : implementiert", content);

            // Verifiziere Assoziationen (Feld- und Property-Abhaengigkeiten)
            Assert.Contains("ChildClass --> BaseClass : nutzt", content);
            Assert.Contains("ChildClass --> IBaseService : nutzt", content);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
