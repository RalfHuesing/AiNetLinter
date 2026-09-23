#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeReferenceRoleTests
{
    [Fact]
    public async Task ScanAsync_ProductionMemberUsedOnlyFromTestProject_ReturnsDeadCodeCandidate()
    {
        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeAdvisoryScannerTests.slnx",
            new ProjectSpec("AiNetLinter", [
                ("MarkdownBuilder.cs", """
                namespace AiNetLinter.Output;
                public sealed class MarkdownBuilder
                {
                    public void BulletList(System.Collections.Generic.IEnumerable<string> items) { }
                }
                """)], VirtualProjectDirectory: "src/AiNetLinter"),
            new ProjectSpec("AiNetLinter.FastTests", [
                ("MarkdownBuilderTests.cs", """
                namespace AiNetLinter.FastTests.Output;
                public sealed class MarkdownBuilderTests
                {
                    public void BulletList_PraefixMinusProElement()
                    {
                        new AiNetLinter.Output.MarkdownBuilder().BulletList([]);
                    }
                }
                """)], ProjectReferences: ["AiNetLinter"], VirtualProjectDirectory: "src/AiNetLinter.FastTests"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.Contains(result.DeadSymbols, symbol =>
            symbol.ContainerType.Contains("MarkdownBuilder")
            && symbol.SymbolName == "BulletList"
            && symbol.Usage == "test_only"
            && symbol.TestReferences == 1
            && symbol.Reason.Contains("1 Testreferenz"));
    }

    [Fact]
    public async Task ScanAsync_ProductionCrossProjectReference_KeepsMemberLive()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("Consumer", [
                ("Consumer.cs", "namespace Consumer; public sealed class Consumer { public void Run() => new Product.Service().Execute(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "src/Consumer"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
    }

    [Fact]
    public async Task ScanAsync_TestFriendReference_ReturnsTestOnlyCandidate()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("AssemblyInfo.cs", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"ProductTests\")]") ,
                ("Service.cs", "namespace Product; internal sealed class Service { internal void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", [
                ("ServiceTests.cs", "namespace ProductTests; public sealed class ServiceTests { public void Run() => new Product.Service().Execute(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        var candidate = Assert.Single(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
        Assert.Equal("test_only", candidate.Usage);
        Assert.Equal(1, candidate.TestReferences);
    }

    [Fact]
    public async Task ScanAsync_ProductionFriendReference_KeepsMemberLive()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("AssemblyInfo.cs", "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(\"ProductConsumer\")]") ,
                ("Service.cs", "namespace Product; internal sealed class Service { internal void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductConsumer", [
                ("Consumer.cs", "namespace ProductConsumer; public sealed class Consumer { public void Run() => new Product.Service().Execute(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "src/ProductConsumer"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
    }

    [Fact]
    public async Task ScanAsync_TestPathReference_DoesNotKeepProductionMemberLive()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }") ,
                ("Tests/ServiceTests.cs", "namespace Product.Tests; public sealed class ServiceTests { public void Run() => new Product.Service().Execute(); }")],
                VirtualProjectDirectory: "src/Product"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        var candidate = Assert.Single(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
        Assert.Equal("test_only", candidate.Usage);
    }

    [Fact]
    public async Task ScanAsync_TestOnlyInterfaceCall_DoesNotKeepImplementationLive()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("IService.cs", "namespace Product; public interface IService { void Execute(); }") ,
                ("Service.cs", "namespace Product; public sealed class Service : IService { public void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", [
                ("ServiceTests.cs", "namespace ProductTests; public sealed class ServiceTests { public void Run(Product.IService service) => service.Execute(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.Contains(result.DeadSymbols, symbol =>
            symbol.SymbolName == "Execute"
            && symbol.ContainerType.Contains("Service")
            && symbol.Usage == "test_only");
    }

    [Fact]
    public async Task ScanAsync_TestOnlyBaseCall_DoesNotKeepOverrideLive()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("BaseService.cs", "namespace Product; public class BaseService { public virtual void Execute() { } }") ,
                ("IntermediateService.cs", "namespace Product; public class IntermediateService : BaseService { public override void Execute() { } }") ,
                ("Service.cs", "namespace Product; public sealed class Service : IntermediateService { public override void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", [
                ("ServiceTests.cs", "namespace ProductTests; public sealed class ServiceTests { public void Run(Product.BaseService service) => service.Execute(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.Contains(result.DeadSymbols, symbol =>
            symbol.SymbolName == "Execute"
            && symbol.ContainerType.Contains("Service")
            && symbol.Usage == "test_only");
    }

    [Fact]
    public async Task ScanAsync_UnknownReferenceRole_DoesNotCreateDeadCodeCandidate()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"));
        var productProject = testSolution.Solution.Projects.Single(project => project.Name == "Product");
        var unknownCaller = productProject.AddDocument(
            "UnmappedCaller.cs",
            SourceText.From("namespace Product; public sealed class UnmappedCaller { public void Run() => new Service().Execute(); }"));

        var result = await ScanMethodsAsync(unknownCaller.Project.Solution);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
        Assert.True(result.Summary.Undecidable > 0);
    }

    [Fact]
    public async Task ScanAsync_LinkedFileInProductionAndTestProjects_ProductionReferenceKeepsMemberLive()
    {
        const string linkedFile = "SharedCaller.cs";
        const string caller = "namespace Product; public sealed class SharedCaller { public void Run() => new Service().Execute(); }";
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }") ,
                (linkedFile, caller)], VirtualProjectDirectory: "src/Linked"),
            new ProjectSpec("ProductTests", [(linkedFile, caller)],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "src/Linked"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        Assert.DoesNotContain(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
    }

    [Fact]
    public async Task ScanAsync_LinkedFileOnlyReferencedFromTestProject_IsTestOnly()
    {
        const string linkedFile = "SharedCaller.cs";
        const string caller = """
            namespace Product;
            public sealed class SharedCaller
            {
            #if !PRODUCTION
                public void Run() => new Service().Execute();
            #endif
            }
            """;
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }") ,
                (linkedFile, caller)], PreprocessorSymbols: ["PRODUCTION"], VirtualProjectDirectory: "src/Linked"),
            new ProjectSpec("ProductTests", [(linkedFile, caller)],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "src/Linked"));

        var result = await ScanMethodsAsync(testSolution.Solution);

        var candidate = Assert.Single(result.DeadSymbols, symbol => symbol.SymbolName == "Execute");
        Assert.Equal("test_only", candidate.Usage);
        Assert.Equal(1, candidate.TestReferences);
    }

    private static RoslynTestSolution CreateSolution(params ProjectSpec[] projects) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeAdvisoryScannerTests.slnx",
            projects);

    private static Task<DeadCodeScanResult> ScanMethodsAsync(Solution solution) =>
        DeadCodeAdvisoryScanner.ScanAsync(
            solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: DeadCodeKindFilter.Method),
            CancellationToken.None);
}
