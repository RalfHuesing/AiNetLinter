#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeBoundaryTests
{
    [Fact]
    public async Task TestReadCountsAsUseAndFieldsAreNotCandidates()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Reads.slnx",
            new ProjectSpec("Host", [("Code.cs", "public sealed class State { public int Value; public int Orphan; }")]),
            new ProjectSpec("Checks", [("Code.cs", "public static class Check { public static int Read(State state) => state.Value; }")], ProjectReferences: ["Host"]));
        var config = TestHelper.CreateDefaultConfig() with { DeadCode = new DeadCodeConfig { ProjectRoles = new() { ["Checks"] = "test" } } };
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Config: config));
        Assert.Empty(result.DeadSymbols);
    }

    [Fact]
    public async Task MixedDeclarationRoleIsUnknownEvenWithoutReferences()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Mixed.slnx",
            new ProjectSpec("Mixed", [("Code.cs", "public sealed class Worker { public void Work() { } }")]));
        var config = TestHelper.CreateDefaultConfig() with { DeadCode = new DeadCodeConfig { ProjectRoles = new() { ["Mixed"] = "unknown" } } };
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Config: config));
        Assert.Empty(result.DeadSymbols);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Work");
    }

    [Fact]
    public async Task PartialTypeIsGroupedOnceAcrossDeclarations()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Partial.slnx",
            new ProjectSpec("Host", [("One.cs", "public partial class Worker { public void First() { } }"),
                ("Two.cs", "// A longer independent declaration must not be consumed while visiting the first syntax tree.\npublic partial class Worker { public void Second() { } }")]));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new());
        var candidate = Assert.Single(result.DeadSymbols);
        Assert.Equal("Worker", candidate.SymbolName);
        Assert.Equal("class", candidate.Kind);
    }

    [Fact]
    public async Task GenericReflectionCalledOnlyByTestsDoesNotProtectProductionProperty()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Roles.slnx",
            new ProjectSpec("Host", [("Code.cs", """
                public sealed class Payload { public int Value { get; set; } }
                public static class Mapper
                {
                    public static object? Read<T>(T value) => typeof(T).GetProperty("Value")!.GetValue(value);
                }
                """)]),
            new ProjectSpec("Checks", [("Code.cs", "public static class Check { public static object? Run() => Mapper.Read(new Payload()); }")],
                ProjectReferences: ["Host"], AdditionalReferences: [MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)]));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new());
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Value");
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ProjectName == "Checks");
    }

    [Fact]
    public async Task ExternalContractImplementationIsProtectedAndExplicitSlotsAreNotCandidates()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Api.slnx",
            new ProjectSpec("Library", [("Code.cs", """
                public interface IApi { void Run(); }
                public sealed class Api : IApi { void IApi.Run() { } private void Orphan() { } }
                """)]));
        var config = TestHelper.CreateDefaultConfig() with { DeadCode = new DeadCodeConfig { DefaultApiSurface = "external_library" } };
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Config: config));
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName.Contains("Run", System.StringComparison.Ordinal));
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
        var closed = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new());
        Assert.DoesNotContain(closed.DeadSymbols, entry => entry.ContainerType == "Api" && entry.SymbolName.Contains("Run", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingFriendConsumerIsUncertainNotUnreferenced()
    {
        using var fixture = RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Friend.slnx",
            new ProjectSpec("Library", [("Code.cs", """
                [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Consumer")]
                public sealed class Api { internal void Extension() { } private void Orphan() { } }
                """)]));
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new());
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Extension");
Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }
}
