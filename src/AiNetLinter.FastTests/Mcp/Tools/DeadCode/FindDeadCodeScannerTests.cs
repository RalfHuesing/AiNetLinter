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

    private static RoslynTestSolution CreateSolution(params (string fileName, string content)[] files) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\FindDeadCodeScannerTests.slnx",
            new ProjectSpec("TestApp", files, VirtualProjectDirectory: "."));
}
