#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.DeadCode;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class FindDeadCodeScannerTests
{
    [Fact]
    public async Task ScanAsync_PrivateUnusedMethod_ReturnsHighConfidenceDeadCode()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                public void DoWork() => System.Console.WriteLine("work");
                private void UnusedHelper() => System.Console.WriteLine("dead");
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.All,
            Confidence: DeadCodeConfidenceFilter.High,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        var dead = Assert.Single(result.DeadSymbols);
        Assert.Equal("UnusedHelper", dead.SymbolName);
        Assert.Equal("method", dead.Kind);
        Assert.Equal("high", dead.Confidence);
        Assert.Equal("private", dead.Accessibility);
    }

    [Fact]
    public async Task ScanAsync_PrivateUsedMethod_ReturnsNoDeadCode()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                public void DoWork() => UsedHelper();
                private void UsedHelper() => System.Console.WriteLine("used");
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.Method);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.Empty(result.DeadSymbols);
    }

    [Fact]
    public async Task ScanAsync_InterfaceImplementation_WithInterfaceCall_NotDeadCode()
    {
        using var testSolution = CreateSolution(
            ("IService.cs", """
            public interface IService
            {
                void Execute();
            }
            """),
            ("Service.cs", """
            public class Service : IService
            {
                public void Execute() => System.Console.WriteLine("executing");
            }
            """),
            ("Consumer.cs", """
            public class Consumer
            {
                public void Run(IService service) => service.Execute();
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.All,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.Method);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.DoesNotContain(result.DeadSymbols, d => d.SymbolName == "Execute" && d.ContainerType.Contains("Service"));
    }

    [Fact]
    public async Task ScanAsync_UtilityPrivateConstructor_IsWhitelisted()
    {
        using var testSolution = CreateSolution(
            ("MathUtils.cs", """
            public static class MathUtils
            {
                public static int Add(int a, int b) => a + b;
            }
            """),
            ("CustomUtils.cs", """
            public class CustomUtils
            {
                private CustomUtils() {}
                public static void Helper() {}
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.All,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.DoesNotContain(result.DeadSymbols, d => d.Kind == "constructor" && d.ContainerType.Contains("CustomUtils"));
    }

    [Fact]
    public async Task ScanAsync_TopDownContainerPruning_MarksContainerAndPrunesInner()
    {
        using var testSolution = CreateSolution(
            ("Container.cs", """
            public class PublicOwner
            {
                public void Work() {}

                private class DeadNested
                {
                    public void DeadInnerMethod() {}
                    public int DeadInnerField = 42;
                }
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.High,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        var dead = Assert.Single(result.DeadSymbols);
        Assert.Equal("DeadNested", dead.SymbolName);
        Assert.Equal("class", dead.Kind);
    }

    [Fact]
    public async Task ScanAsync_FilterAccessibility_ReturnsOnlyRequestedAccessibility()
    {
        using var testSolution = CreateSolution(
            ("Sample.cs", """
            public class Sample
            {
                private void DeadPrivate() {}
                internal void DeadInternal() {}
                public void DeadPublic() {}
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.Method);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        var dead = Assert.Single(result.DeadSymbols);
        Assert.Equal("DeadPrivate", dead.SymbolName);
    }

    [Fact]
    public async Task ScanAsync_ModeLocals_CollectsUnusedFieldDiagnostics()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                private int _unusedValue;
                public void DoWork() => System.Console.WriteLine("hi");
            }
            """));

        var args = new FindDeadCodeArgs(
            Mode: DeadCodeMode.Locals,
            Accessibility: DeadCodeAccessibilityFilter.All,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        var dead = Assert.Single(result.DeadSymbols);
        Assert.Equal("_unusedValue", dead.SymbolName);
        Assert.Equal("field", dead.Kind);
        Assert.Equal("high", dead.Confidence);
        Assert.Contains("CS0169", dead.Reason);
    }

    [Fact]
    public async Task ScanAsync_ModeBoth_CombinesAndDeduplicates()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                private int _unusedValue;
                private void DeadMethod() {}
                public void DoWork() => System.Console.WriteLine("hi");
            }
            """));

        var args = new FindDeadCodeArgs(
            Mode: DeadCodeMode.Both,
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.High,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.Contains(result.DeadSymbols, d => d.SymbolName == "_unusedValue");
        Assert.Contains(result.DeadSymbols, d => d.SymbolName == "DeadMethod");
    }

    [Fact]
    public async Task ScanAsync_WithMaxResults_TruncatesAndSetsFlag()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                private void Dead1() {}
                private void Dead2() {}
                private void Dead3() {}
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.Method,
            MaxResults: 2);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.True(result.IsTruncated);
        Assert.Equal(2, result.DeadSymbols.Count);
        Assert.Equal(3, result.Summary.TotalDead);
    }

    [Fact]
    public async Task ScanAsync_WithScopeFilter_LimitsToMatchingFiles()
    {
        using var testSolution = CreateSolution(
            ("Included.cs", """
            public class IncludedService
            {
                private void DeadIncluded() {}
            }
            """),
            ("Excluded.cs", """
            public class ExcludedService
            {
                private void DeadExcluded() {}
            }
            """));

        var args = new FindDeadCodeArgs(
            ScopeFilter: "Included",
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.Method);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        var dead = Assert.Single(result.DeadSymbols);
        Assert.Equal("DeadIncluded", dead.SymbolName);
    }

    [Fact]
    public async Task ScanAsync_JsonConstructorAttribute_IsWhitelisted()
    {
        using var testSolution = CreateSolution(
            ("Model.cs", """
            namespace System.Text.Json.Serialization
            {
                [AttributeUsage(AttributeTargets.Constructor)]
                public sealed class JsonConstructorAttribute : Attribute {}
            }

            public class Model
            {
                [System.Text.Json.Serialization.JsonConstructor]
                private Model(int id) {}
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.All,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.DoesNotContain(result.DeadSymbols, d => d.Kind == "constructor" && d.ContainerType.Contains("Model"));
    }

    [Fact]
    public async Task ScanAsync_UnusedEventAndDelegate_DetectedAsDeadCode()
    {
        using var testSolution = CreateSolution(
            ("Service.cs", """
            public class Service
            {
                private delegate void DeadCallback(int x);
                private event System.Action? DeadEvent;
                public void DoWork() => System.Console.WriteLine("hi");
            }
            """));

        var args = new FindDeadCodeArgs(
            Accessibility: DeadCodeAccessibilityFilter.Private,
            Confidence: DeadCodeConfidenceFilter.Both,
            Kind: DeadCodeKindFilter.All);

        var result = await FindDeadCodeScanner.ScanAsync(testSolution.Solution, args, CancellationToken.None);

        Assert.Contains(result.DeadSymbols, d => d.SymbolName == "DeadCallback" && d.Kind == "delegate");
        Assert.Contains(result.DeadSymbols, d => d.SymbolName == "DeadEvent" && d.Kind == "event");
    }

    private static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FindDeadCodeScannerTests.slnx",
            new ProjectSpec("TestApp", files, VirtualProjectDirectory: "."));
}
