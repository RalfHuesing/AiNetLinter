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
            new ProjectSpec("ProductTests", AdditionalReferences: [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)], Documents: [
                ("QueryTests.cs", """
                namespace ProductTests;
                public sealed class QueryTests
                {
                    public Product.Query Create() => new Product.Query();
                }
                """)], ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanAllAsync(testSolution.Solution);

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.Kind == "class" && entry.SymbolName == "Query");
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.Kind == "constructor");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "QueryHandler");
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_ReportsOnlyDeadTypesAndOrdinaryMethodsAndTreatsTestReferencesAsUse()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Candidates.cs", """
                namespace Product;
                public sealed class UnreferencedType { }
                public sealed class UsedByTest { public void Run() { } }
                public sealed class Service
                {
                    public void Convert(int value) { }
                    public void Convert(string value) { }
                    public void Keep(int value) => Convert(value);
                    public string Wire { get; set; } = "";
                    public const int Constant = 1;
                    private int _state;
                }
                public sealed class ServiceCaller { public void Call() => new Service().Keep(1); }
                """)], VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", AdditionalReferences: [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)], Documents: [
                ("ServiceTests.cs", "namespace ProductTests; public sealed class ServiceTests { public void Run() => new Product.UsedByTest().Run(); }")],
                ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanAllAsync(testSolution.Solution);

        Assert.Contains(result.DeadSymbols, entry => entry.Kind == "class" && entry.SymbolName == "UnreferencedType");
        Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "Convert");
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName is "Wire" or "Constant" or "_state" or "UsedByTest");
    }

    [Fact]
    public async Task ScanAsync_TestSupportProjectIsNeverASourceOfCandidates()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Service.cs", "namespace Product; public sealed class Service { public void Execute() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("Tests.Support", [
                ("Fixture.cs", "namespace Tests.Support; public sealed class Fixture { public void Helper() { } }")],
                VirtualProjectDirectory: "tests/Support"));
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new AiNetLinter.Configuration.DeadCodeConfig
            {
                ProjectRoles = new System.Collections.Generic.Dictionary<string, string> { ["Tests.Support"] = "test" }
            }
        };

        var result = await DeadCodeAdvisoryScanner.ScanAsync(testSolution.Solution,
            new(Config: config));

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ProjectName == "Tests.Support");
    }

    [Fact]
    public async Task ScanAsync_DataMembersAreNeverReportedAsDeadCodeCandidates()
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

        var result = await ScanAsync(testSolution.Solution);

        Assert.Empty(result.DeadSymbols);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_PrivateFieldsAndPropertiesAreNotAdvisoryCandidates()
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
                ),
            CancellationToken.None);

        Assert.Empty(result.DeadSymbols);
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_PublicWirePropertyIsNotAnAdvisoryCandidate()
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

        var result = await ScanAsync(testSolution.Solution);

        Assert.Empty(result.DeadSymbols);
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

        var result = await ScanAsync(testSolution.Solution);

        var callback = Assert.Single(result.DeadSymbols, entry => entry.SymbolName == "Initialize");
        Assert.Contains("publicApiSurface", callback.LimitsApplies);
        Assert.Contains("reflection", callback.LimitsApplies);
        Assert.Contains(callback.Countercheck!, check => check.Contains("Consumer", StringComparison.OrdinalIgnoreCase));
        Assert.False(result.DeletionClaim);
    }

    [Fact]
    public async Task ScanAsync_TestOnlyInterfaceCallSuppressesProductionCandidates()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("IProcessor.cs", "namespace Product; public interface IProcessor { void Process(); }"),
                ("Processor.cs", "namespace Product; public sealed class Processor : IProcessor { public void Process() { } }")],
                VirtualProjectDirectory: "src/Product"),
            new ProjectSpec("ProductTests", AdditionalReferences: [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(FactAttribute).Assembly.Location)], Documents: [
                ("ProcessorTests.cs", """
                namespace ProductTests;
                public sealed class ProcessorTests
                {
                    public void Exercise(Product.IProcessor processor) => processor.Process();
                }
                """)], ProjectReferences: ["Product"], VirtualProjectDirectory: "tests/ProductTests"));

        var result = await ScanAllAsync(testSolution.Solution);

        Assert.Empty(result.DeadSymbols);
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

        var result = await ScanAsync(testSolution.Solution);

        var dynamicCandidate = Assert.Single(result.DeadSymbols, entry =>
            entry.ContainerType == "DynamicTarget" && entry.SymbolName == "Dispatch");
        Assert.DoesNotContain(result.DeadSymbols, entry =>
            entry.ContainerType == "ReflectedTarget" && entry.SymbolName == "Invoke");

        Assert.Contains("reflection", dynamicCandidate.LimitsApplies);
        Assert.Contains(dynamicCandidate.Countercheck!, item => item.Equals("Dynamic", StringComparison.OrdinalIgnoreCase));
Assert.Contains("statische Referenzsuche", dynamicCandidate.EvidenceBoundary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reflection", result.RecommendedNextAction.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.DeletionClaim);
    }

    private static RoslynTestSolution CreateSolution(params ProjectSpec[] projects) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeAdvisoryPrecisionContractTests.slnx",
            projects);

    private static Task<DeadCodeScanResult> ScanAllAsync(Solution solution) =>
        ScanAsync(solution);

    private static Task<DeadCodeScanResult> ScanAsync(Solution solution) =>
        DeadCodeAdvisoryScanner.ScanAsync(
            solution,
            new DeadCodeAdvisoryOptions(),
            CancellationToken.None);
}
