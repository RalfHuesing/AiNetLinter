#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Metrics;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Metrics;

[Trait("Category", "Unit")]
public sealed class AIContextFootprintCalculatorTests
{
    [Fact]
    public async Task IsDeclarationOnlyType_IdentifiesDtoAndRecordsCorrectly()
    {
        const string source = """
            namespace Sample;

            public class UserDto
            {
                public string Name { get; set; } = "";
                public int Age { get; set; }
            }

            public record PositionalRecord(string Id, string Value);

            public enum Status
            {
                Active,
                Inactive
            }

            public class ServiceClass
            {
                public void DoWork() { }
            }

            public record RecordWithMethods(string Id)
            {
                public void CustomAction() { }
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FootprintTests.slnx",
            new ProjectSpec("TestProject", [("Types.cs", source)]));

        var project = testSolution.Solution.Projects.First();
        var compilation = await project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var userDto = compilation.GetTypeByMetadataName("Sample.UserDto");
        var positionalRecord = compilation.GetTypeByMetadataName("Sample.PositionalRecord");
        var statusEnum = compilation.GetTypeByMetadataName("Sample.Status");
        var serviceClass = compilation.GetTypeByMetadataName("Sample.ServiceClass");
        var recordWithMethods = compilation.GetTypeByMetadataName("Sample.RecordWithMethods");

        Assert.NotNull(userDto);
        Assert.NotNull(positionalRecord);
        Assert.NotNull(statusEnum);
        Assert.NotNull(serviceClass);
        Assert.NotNull(recordWithMethods);

        Assert.True(AIContextFootprintCalculator.IsDeclarationOnlyType(userDto!));
        Assert.True(AIContextFootprintCalculator.IsDeclarationOnlyType(positionalRecord!));
        Assert.True(AIContextFootprintCalculator.IsDeclarationOnlyType(statusEnum!));
        Assert.False(AIContextFootprintCalculator.IsDeclarationOnlyType(serviceClass!));
        Assert.False(AIContextFootprintCalculator.IsDeclarationOnlyType(recordWithMethods!));
    }

    [Fact]
    public async Task Calculate_WithDeclarationOnlyDto_CapsDtoFootprintToMaxDeclarationLines()
    {
        const string serviceSource = """
            namespace Sample;

            public class ConsumerService
            {
                public UserDto Process(UserDto input)
                {
                    return input;
                }
            }
            """;

        // Create a DTO file with 50 lines of padding/comments
        var dtoSource = "namespace Sample;\n\n" +
            "public class UserDto\n{\n    public string Name { get; set; }\n    public int Age { get; set; }\n}\n" +
            string.Join("\n", Enumerable.Range(1, 40).Select(i => $"// padding line {i}"));

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FootprintTests.slnx",
            new ProjectSpec("TestProject", [
                ("ConsumerService.cs", serviceSource),
                ("UserDto.cs", dtoSource)
            ]));

        var project = testSolution.Solution.Projects.First();
        var compilation = await project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var serviceSymbol = compilation.GetTypeByMetadataName("Sample.ConsumerService");
        Assert.NotNull(serviceSymbol);

        var (totalLines, topDeps) = AIContextFootprintCalculator.CalculateDetailed(serviceSymbol!);

        var serviceFileLines = serviceSource.Split('\n').Length;
        // UserDto declaration span is 5 lines (<= MaxDeclarationLines 10), far less than the 47 lines in UserDto.cs
        Assert.True(totalLines < serviceFileLines + 40);
        Assert.Equal(serviceFileLines + 5, totalLines);
        Assert.Contains(topDeps, d => d.Name.Contains("UserDto") && d.Lines == 5);
    }

    [Fact]
    public async Task Calculate_WithMethodClass_CountsFullFileLines()
    {
        const string callerSource = """
            namespace Sample;

            public class CallerClass
            {
                private readonly WorkerClass _worker = new();
                public void Run() => _worker.Execute();
            }
            """;

        const string workerSource = """
            namespace Sample;

            public class WorkerClass
            {
                public void Execute()
                {
                    // line 1
                    // line 2
                    // line 3
                }
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FootprintTests.slnx",
            new ProjectSpec("TestProject", [
                ("CallerClass.cs", callerSource),
                ("WorkerClass.cs", workerSource)
            ]));

        var project = testSolution.Solution.Projects.First();
        var compilation = await project.GetCompilationAsync();
        Assert.NotNull(compilation);

        var callerSymbol = compilation.GetTypeByMetadataName("Sample.CallerClass");
        Assert.NotNull(callerSymbol);

        var (totalLines, topDeps) = AIContextFootprintCalculator.CalculateDetailed(callerSymbol!);

        var callerLines = callerSource.Split('\n').Length;
        var workerLines = workerSource.Split('\n').Length;

        Assert.Equal(callerLines + workerLines, totalLines);
        Assert.Contains(topDeps, d => d.Name.Contains("WorkerClass") && d.Lines == workerLines);
    }
}
