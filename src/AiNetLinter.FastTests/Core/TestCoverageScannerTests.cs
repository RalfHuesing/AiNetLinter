#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class TestCoverageScannerTests
{
    [Fact]
    public async Task FindTestsForSymbolAsync_NamingConvention_FindsMatchingTests()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", """
                    namespace App;
                    public class Calculator
                    {
                        public int Add(int a, int b) => a + b;
                    }
                    """)
            ]),
            new ProjectSpec("App.Tests", [
                ("CalculatorTests.cs", """
                    namespace App.Tests;
                    public class CalculatorTests
                    {
                        [Xunit.Fact]
                        public void Add_ReturnsSum()
                        {
                            var calc = new App.Calculator();
                            _ = calc.Add(1, 2);
                        }

                        [Xunit.Fact]
                        public void Subtract_Test()
                        {
                        }
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var calcType = compilation!.GetTypeByMetadataName("App.Calculator")!;
        var addMethod = calcType.GetMembers().OfType<IMethodSymbol>().First(m => m.Name == "Add");

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(addMethod, solutionOwner.Solution, CancellationToken.None);

        Assert.True(result.TotalMatchingTests >= 1);
        var testFile = Assert.Single(result.TestFiles);
        Assert.Equal("CalculatorTests", testFile.TestClassName);
        Assert.Contains("Add_ReturnsSum", testFile.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_CoversComment_FindsMatchingTests()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App", [
                ("WorkerService.cs", """
                    namespace App;
                    public class WorkerService
                    {
                        public void DoWork() {}
                    }
                    """)
            ]),
            new ProjectSpec("App.Tests", [
                ("CustomWorkerTestFile.cs", """
                    // @covers WorkerService
                    namespace App.Tests;
                    public class CustomWorkerTestFile
                    {
                        [Xunit.Fact]
                        public void TestWorkerExecution()
                        {
                        }
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var workerType = compilation!.GetTypeByMetadataName("App.WorkerService")!;

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(workerType, solutionOwner.Solution, CancellationToken.None);

        Assert.Equal(1, result.TotalMatchingTests);
        var testFile = Assert.Single(result.TestFiles);
        Assert.Equal("Explicit @covers Comment", testFile.MatchReason);
        Assert.Contains("TestWorkerExecution", testFile.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_TypeofReference_FindsMatchingTests()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App", [
                ("ConfigValidator.cs", """
                    namespace App;
                    public class ConfigValidator
                    {
                    }
                    """)
            ]),
            new ProjectSpec("App.Tests", [
                ("IntegrationSmokeTests.cs", """
                    namespace App.Tests;
                    public class IntegrationSmokeTests
                    {
                        [Xunit.Fact]
                        public void TestValidatorType()
                        {
                            _ = typeof(App.ConfigValidator);
                        }
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var validatorType = compilation!.GetTypeByMetadataName("App.ConfigValidator")!;

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(validatorType, solutionOwner.Solution, CancellationToken.None);

        Assert.Equal(1, result.TotalMatchingTests);
        var testFile = Assert.Single(result.TestFiles);
        Assert.Equal("Direct typeof Reference", testFile.MatchReason);
        Assert.Contains("TestValidatorType", testFile.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_NoMatchingTests_ReturnsEmpty()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App", [
                ("UnusedHelper.cs", """
                    namespace App;
                    public class UnusedHelper
                    {
                    }
                    """)
            ]),
            new ProjectSpec("App.Tests", [
                ("OtherTests.cs", """
                    namespace App.Tests;
                    public class OtherTests
                    {
                        [Xunit.Fact]
                        public void TestSomethingElse() {}
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var helperType = compilation!.GetTypeByMetadataName("App.UnusedHelper")!;

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(helperType, solutionOwner.Solution, CancellationToken.None);

        Assert.Equal(0, result.TotalMatchingTests);
        Assert.Empty(result.TestFiles);
    }
}
