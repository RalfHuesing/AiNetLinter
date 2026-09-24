#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeAdvisoryPrecisionContractTests
{
    [Fact]
    public async Task ScanAsync_ProductiveTypeReferenceDoesNotBecomeProductiveConstructorReference()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Query.cs", """
                namespace Product;
                public sealed class Query
                {
                    public Query() { }
                }
                """),
                ("QueryHandler.cs", """
                namespace Product;
                public sealed class QueryHandler
                {
                    public void Handle(Query query) { }
                }
                """)], VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", [
                ("QueryTests.cs", """
                namespace ProductTests;
                public sealed class QueryTests
                {
                    public Product.Query Create() => new Product.Query();
                }
                """)], ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanAllAsync(testSolution.Solution);

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.Kind == "class" && entry.SymbolName == "Query");
        var constructor = Assert.Single(result.DeadSymbols, entry =>
            entry.Kind == "constructor" && entry.ContainerType == "Product.Query");
        Assert.Equal("test_only", constructor.Usage);
        Assert.Equal(1, constructor.TestReferences);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_WriteOnlyPropertyReferenceIsNotReportedAsUnreferenced()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("TestApp", [("Models.cs", """
            public sealed class Record
            {
                public string WrittenOnly { get; set; } = "";
                public string NeverTouched { get; set; } = "";
            }

            public sealed class RecordUpdater
            {
                public void Update(Record record, string value) => record.WrittenOnly = value;
            }
            """)], VirtualProjectDirectory: "src/TestApp"));

        var result = await ScanAsync(testSolution.Solution, DeadCodeKindFilter.Property);

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "WrittenOnly");
        var candidate = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "NeverTouched");
        Assert.Equal("unreferenced", candidate.Usage);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_AssignedOnlyPrivateFieldIsReportedByDiagnosticWhileWriteOnlyPropertyStaysUnclassified()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("State.cs", """
            namespace Product;
            public sealed class State
            {
                private int _assignedOnly = 1;
                private int AssignedOnlyProperty { get; set; }

                public void Update(int value)
                {
                    AssignedOnlyProperty = value;
                }
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var compilation = await testSolution.Solution.Projects.Single().GetCompilationAsync();
        Assert.Contains(compilation!.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0414");

        var result = await DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.Private,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: DeadCodeKindFilter.All,
                Mode: DeadCodeMode.Both),
            CancellationToken.None);

        var field = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "_assignedOnly");
        Assert.Equal("field", field.Kind);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "AssignedOnlyProperty");
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_PublicPropertyUsedOnlyBySerializerRemainsAnUncertainSameSymbolCandidate()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("Payload.cs", """
            namespace Product;
            public sealed class Payload
            {
                public string Value { get; init; } = "";
            }

            public static class Serializer
            {
                public static string Serialize(Payload payload) =>
                    System.Text.Json.JsonSerializer.Serialize(payload);
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution.Solution, DeadCodeKindFilter.Property);

        var property = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "Value");
        Assert.Equal("unreferenced", property.Usage);
        Assert.Equal("low", property.Confidence);
        Assert.Contains("jsonSerializer", property.LimitsApplies);
        Assert.Contains("reflection", property.LimitsApplies);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_UnreferencedPublicCallbackConventionRemainsAnAdvisoryWithExternalUseLimits()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("Callbacks.cs", """
            namespace Product;
            [System.AttributeUsage(System.AttributeTargets.Method)]
            public sealed class RuntimeCallbackAttribute : System.Attribute { }

            public sealed class CallbackTarget
            {
                [RuntimeCallback]
                public void Initialize() { }
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution.Solution, DeadCodeKindFilter.Method);

        var callback = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "Initialize");
        Assert.Equal("unreferenced", callback.Usage);
        Assert.Equal("low", callback.Confidence);
        Assert.Contains("publicApiSurface", callback.LimitsApplies);
        Assert.Contains("reflection", callback.LimitsApplies);
        Assert.Contains(callback.Countercheck!, check => check.Contains("Consumer", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_TestOnlyInterfaceCallKeepsImplementationTestOnlyAndReportsInterfaceBoundary()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("IProcessor.cs", "namespace Product; public interface IProcessor { void Process(); }"),
                ("Processor.cs", "namespace Product; public sealed class Processor : IProcessor { public void Process() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", [
                ("ProcessorTests.cs", """
                namespace ProductTests;
                public sealed class ProcessorTests
                {
                    public void Exercise(Product.IProcessor processor) => processor.Process();
                }
                """)], ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanAllAsync(testSolution.Solution);

        var implementation = Assert.Single(result.DeadSymbols, entry =>
            entry.Kind == "method" && entry.ContainerType == "Product.Processor" && entry.SymbolName == "Process");
        Assert.Equal("test_only", implementation.Usage);
        Assert.Equal(1, implementation.TestReferences);
        Assert.Contains("interfaceImplementation", implementation.LimitsApplies);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_DynamicAndStringBasedReflectionRemainAdvisoryCandidatesWithCounterchecks()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("TestApp", [("Targets.cs", """
            public sealed class DynamicTarget
            {
                public void Dispatch() { }
            }

            public sealed class ReflectedTarget
            {
                public void Invoke() { }
            }

            public sealed class DynamicCaller
            {
                public void Run(dynamic target) => target.Dispatch();
            }

            public sealed class ReflectionCaller
            {
                public System.Reflection.MethodInfo? Find() => typeof(ReflectedTarget).GetMethod("Invoke");
            }
            """)], VirtualProjectDirectory: "src/TestApp"));

        var result = await ScanAsync(testSolution.Solution, DeadCodeKindFilter.Method);

        var dynamicCandidate = Assert.Single(result.DeadSymbols, entry =>
            entry.ContainerType == "DynamicTarget" && entry.SymbolName == "Dispatch");
        var reflectionCandidate = Assert.Single(result.DeadSymbols, entry =>
            entry.ContainerType == "ReflectedTarget" && entry.SymbolName == "Invoke");

        Assert.Equal("unreferenced", dynamicCandidate.Usage);
        Assert.Equal("unreferenced", reflectionCandidate.Usage);
        Assert.Equal("low", dynamicCandidate.Confidence);
        Assert.Equal("low", reflectionCandidate.Confidence);
        Assert.Contains("reflection", dynamicCandidate.LimitsApplies);
        Assert.Contains("reflection", reflectionCandidate.LimitsApplies);
        Assert.Contains(dynamicCandidate.Countercheck!, item => item.Equals("Dynamic", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(reflectionCandidate.Countercheck!, item => item.Equals("Reflection", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("statische Referenzsuche", dynamicCandidate.EvidenceBoundary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reflection", result.RecommendedNextAction.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.DeletionClaim);
    }

    private static RoslynTestSolution CreateSolution(params ProjectSpec[] projects) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeAdvisoryPrecisionContractTests.slnx",
            projects);

    private static Task<DeadCodeScanResult> ScanAllAsync(Solution solution) =>
        ScanAsync(solution, DeadCodeKindFilter.All);

    private static Task<DeadCodeScanResult> ScanAsync(Solution solution, DeadCodeKindFilter kind) =>
        DeadCodeAdvisoryScanner.ScanAsync(
            solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: kind),
            CancellationToken.None);
}
