#nullable enable

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Core;
using AiNetLinter.Mcp.Tools.TestContext;
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
        Assert.Empty(testFile.TestMethods);
        Assert.Equal(TestEvidenceKind.ExplicitTypeCoverage, testFile.EvidenceKind);
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
        Assert.Equal(TestEvidenceKind.ExplicitTypeCoverage, testFile.EvidenceKind);
        Assert.Equal("medium", testFile.Confidence);
        Assert.Empty(testFile.TestMethods);
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

    [Fact]
    public async Task FindTestsForSymbolAsync_TypeUsageInTestClassWithDifferentName_FindsCallingTests()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App", [
                ("SqlPackSelector.cs", """
                    namespace App;
                    public class SqlPackSelector
                    {
                        public void Select() {}
                    }
                    """)
            ]),
            new ProjectSpec("App.Tests", [
                ("FeatureAndConnectionTests.cs", """
                    namespace App.Tests;
                    public class FeatureAndConnectionTests
                    {
                        [Xunit.Fact]
                        public void TestSelectorInvocation()
                        {
                            var selector = new App.SqlPackSelector();
                            selector.Select();
                        }

                        [Xunit.Fact]
                        public void OtherTest() {}
                    }
                    """)
            ], ["App"])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var selectorType = compilation!.GetTypeByMetadataName("App.SqlPackSelector")!;

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(selectorType, solutionOwner.Solution, CancellationToken.None);

        Assert.True(result.TotalMatchingTests >= 1);
        var testFile = Assert.Single(result.TestFiles);
        Assert.Equal("FeatureAndConnectionTests", testFile.TestClassName);
        Assert.Empty(testFile.TestMethods);
        Assert.Equal(TestEvidenceKind.DirectTypeUse, testFile.EvidenceKind);
        Assert.Equal("high", testFile.Confidence);
        Assert.Equal(TestCoverageMatchReasons.DirectTypeUsage, testFile.MatchReason);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_TypeNamingConvention_DoesNotClaimMemberMethods()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ConventionOnly.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("CalculatorTests.cs", """
                    namespace App.Tests;
                    public class CalculatorTests
                    {
                        [Xunit.Fact] public void Case01() { }
                        [Xunit.Fact] public void Case02() { }
                        [Xunit.Fact] public void Case03() { }
                        [Xunit.Fact] public void Case04() { }
                        [Xunit.Fact] public void Case05() { }
                        [Xunit.Fact] public void Case06() { }
                        [Xunit.Fact] public void Case07() { }
                        [Xunit.Fact] public void Case08() { }
                        [Xunit.Fact] public void Case09() { }
                        [Xunit.Fact] public void Case10() { }
                        [Xunit.Fact] public void Case11() { }
                        [Xunit.Fact] public void Case12() { }
                        [Xunit.Fact] public void Case13() { }
                        [Xunit.Fact] public void Case14() { }
                        [Xunit.Fact] public void Case15() { }
                        [Xunit.Fact] public void Case16() { }
                        [Xunit.Fact] public void Case17() { }
                        [Xunit.Fact] public void Case18() { }
                        [Xunit.Fact] public void Case19() { }
                        [Xunit.Fact] public void Case20() { }
                        [Xunit.Fact] public void Case21() { }
                        [Xunit.Fact] public void Case22() { }
                        [Xunit.Fact] public void Case23() { }
                        [Xunit.Fact] public void Case24() { }
                        [Xunit.Fact] public void Case25() { }
                    }
                    """)
            ], ["App"])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var calculator = compilation!.GetTypeByMetadataName("App.Calculator")!;

        var result = await TestCoverageScanner.FindTestsForSymbolAsync(
            calculator, solutionOwner.Solution, CancellationToken.None);

        var file = Assert.Single(result.TestFiles);
        Assert.Equal(25, result.TotalMatchingTests);
        Assert.Empty(file.TestMethods);
        Assert.Equal(TestEvidenceKind.TypeNamingConvention, file.EvidenceKind);
        Assert.Equal("low", file.Confidence);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_DirectInvocation_ReportsHighEvidenceAndOnlyInvokedMethod()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Evidence.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("CalculatorTests.cs", """
                    namespace App.Tests;
                    public class CalculatorTests
                    {
                        [Xunit.Fact] public void Add_ReturnsValue() { _ = new App.Calculator().Add(); }
                        [Xunit.Fact] public void Add_UnrelatedCase() { }
                    }
                    """)
            ], ["App"])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var add = compilation!.GetTypeByMetadataName("App.Calculator")!.GetMembers("Add").Single();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(add, solutionOwner.Solution, CancellationToken.None);

        var file = Assert.Single(result.TestFiles);
        Assert.Equal(TestEvidenceKind.DirectInvocation, file.EvidenceKind);
        Assert.Equal("high", file.Confidence);
        Assert.Equal(["Add_ReturnsValue"], file.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_ExplicitMemberCoverage_ReportsOnlyAnnotatedMethod()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\ExplicitMember.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("CalculatorTests.cs", """
                    namespace App.Tests;
                    public class CalculatorTests
                    {
                        // @covers App.Calculator.Add
                        [Xunit.Fact] public void CoversAdd() { }
                        [Xunit.Fact] public void OtherCase() { }
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var add = compilation!.GetTypeByMetadataName("App.Calculator")!.GetMembers("Add").Single();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(add, solutionOwner.Solution, CancellationToken.None);

        var file = Assert.Single(result.TestFiles);
        Assert.Equal(TestEvidenceKind.ExplicitMemberCoverage, file.EvidenceKind);
        Assert.Equal(["CoversAdd"], file.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_MemberNameMatch_ReportsMediumEvidence()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MemberName.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("OtherTests.cs", """
                    namespace App.Tests;
                    public class OtherTests
                    {
                        [Xunit.Fact] public void Add_ReturnsValue() { }
                        [Xunit.Fact] public void OtherCase() { }
                    }
                    """)
            ])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var add = compilation!.GetTypeByMetadataName("App.Calculator")!.GetMembers("Add").Single();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(add, solutionOwner.Solution, CancellationToken.None);

        var file = Assert.Single(result.TestFiles);
        Assert.Equal(TestEvidenceKind.MemberNameMatch, file.EvidenceKind);
        Assert.Equal("medium", file.Confidence);
        Assert.Equal(["Add_ReturnsValue"], file.TestMethods);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_MultipleTestClassesInOneFile_CollectsAllClassesAndDeduplicatesRecommendation()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\MultiClassFile.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("CalculatorTests.cs", """
                    namespace App.Tests;
                    public class CalculatorTests
                    {
                        [Xunit.Fact] public void Add_First() { _ = new App.Calculator().Add(); }
                    }
                    public class CalculatorMoreTests
                    {
                        [Xunit.Fact] public void Add_Second() { _ = new App.Calculator().Add(); }
                    }
                    public class UnrelatedTests
                    {
                        [Xunit.Fact] public void Other_First() { }
                        [Xunit.Fact] public void Other_Second() { }
                    }
                    """)
            ], ["App"])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var add = compilation!.GetTypeByMetadataName("App.Calculator")!.GetMembers("Add").Single();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(add, solutionOwner.Solution, CancellationToken.None);

        var file = Assert.Single(result.TestFiles);
        Assert.Equal(["CalculatorMoreTests", "CalculatorTests"], file.TestClassNames);
        Assert.Equal(2, result.TotalMatchingTests);
        Assert.Equal(2, file.TotalClassTests);
        var command = Assert.Single(TestRecommendationBuilder.BuildDotNetTestCommands(result.TestFiles));
        Assert.Equal(1, command.Split("FullyQualifiedName~CalculatorTests", System.StringSplitOptions.None).Length - 1);
        Assert.Equal(1, command.Split("FullyQualifiedName~CalculatorMoreTests", System.StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task FindTestsForSymbolAsync_FilePathOrderingUsesOrdinalComparer()
    {
        using var solutionOwner = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\OrdinalPaths.slnx",
            new ProjectSpec("App", [
                ("Calculator.cs", "namespace App; public class Calculator { public int Add() => 1; }")
            ]),
            new ProjectSpec("App.Tests", [
                ("aTests.cs", "namespace App.Tests; public class ATests { [Xunit.Fact] public void Add_A() { _ = new App.Calculator().Add(); } }") ,
                ("BTests.cs", "namespace App.Tests; public class BTests { [Xunit.Fact] public void Add_B() { _ = new App.Calculator().Add(); } }")
            ], ["App"])
        );

        var compilation = await solutionOwner.Solution.Projects.First(p => p.Name == "App").GetCompilationAsync();
        var add = compilation!.GetTypeByMetadataName("App.Calculator")!.GetMembers("Add").Single();
        var result = await TestCoverageScanner.FindTestsForSymbolAsync(add, solutionOwner.Solution, CancellationToken.None);

        Assert.Equal("BTests.cs", System.IO.Path.GetFileName(result.TestFiles[0].FilePath));
        Assert.Equal("aTests.cs", System.IO.Path.GetFileName(result.TestFiles[1].FilePath));
    }
}
