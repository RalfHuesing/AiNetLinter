#nullable enable

using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class TestDetectorTests
{
    [Theory]
    [InlineData("src/MyProject.Tests/UnitTest1.cs", true)]
    [InlineData("tests/MyProject/CalculatorTests.cs", true)]
    [InlineData("test/MyProject/CalculatorSpec.cs", true)]
    [InlineData("src/MyProject.FastTests/SomeTest.cs", true)]
    [InlineData("src/MyProject.IntegrationTests/DbTests.cs", true)]
    [InlineData("src/MyProject.ComponentTests/ApiTest.cs", true)]
    [InlineData("src/MyProject.UnitTests/CoreTest.cs", true)]
    [InlineData("src/MyProject.Specs/FeatureSpecs.cs", true)]
    [InlineData("src/MyProject/Services/OrderService.cs", false)]
    [InlineData("src/MyProject/Controllers/HomeController.cs", false)]
    [InlineData("", false)]
    public void IsTestFile_DetectsTestPaths(string path, bool expected)
    {
        var result = TestDetector.IsTestFile(path);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("CalculatorTests", "Calculator", true)]
    [InlineData("CalculatorTest", "Calculator", true)]
    [InlineData("TestCalculator", "Calculator", true)]
    [InlineData("CalculatorIntegrationTests", "Calculator", true)]
    [InlineData("CalculatorUnitTests", "Calculator", true)]
    [InlineData("CalculatorSpecs", "Calculator", true)]
    [InlineData("OrderServiceTests", "Calculator", false)]
    public void MatchesTestClassName_ValidatesConventions(string testClassName, string targetTypeName, bool expected)
    {
        var result = TestDetector.MatchesTestClassName(testClassName, targetTypeName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("[Fact] public void Test1() {}", true)]
    [InlineData("[Theory] public void Test2() {}", true)]
    [InlineData("[Test] public void Test3() {}", true)]
    [InlineData("[TestMethod] public void Test4() {}", true)]
    [InlineData("[TestCase(1)] public void Test5(int x) {}", true)]
    [InlineData("[NUnit.Framework.Test] public void Test6() {}", true)]
    [InlineData("public void HelperMethod() {}", false)]
    public void IsTestMethod_DetectsTestAttributes(string methodCode, bool expected)
    {
        var tree = CSharpSyntaxTree.ParseText($"class Dummy {{ {methodCode} }}");
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        var result = TestDetector.IsTestMethod(method);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("[Trait(\"Category\", \"Integration\")] class T {}", "Unit/File.cs", "Integration")]
    [InlineData("[NUnit.Framework.Category(\"Component\")] class T {}", "Unit/File.cs", "Component")]
    [InlineData("[Microsoft.VisualStudio.TestTools.UnitTesting.TestCategory(\"Custom\")] class T {}", "Unit/File.cs", "Custom")]
    [InlineData("class T {}", "tests/Project.IntegrationTests/DbTest.cs", "Integration")]
    [InlineData("class T {}", "tests/Project.ComponentTests/ApiTest.cs", "Component")]
    [InlineData("class T {}", "tests/Project.UnitTests/CoreTest.cs", "Unit")]
    public void DetermineCategory_ResolvesCategory(string code, string path, string expectedCategory)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var result = TestDetector.DetermineCategory(tree.GetRoot(), path);
        Assert.Equal(expectedCategory, result);
    }

    [Fact]
    public void IsTestProject_DetectsTestProjectByReferencesAndName()
    {
        using var solution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\virtual\Solution.slnx",
            new ProjectSpec("App.Core", [
                ("Service.cs", "public class Service {}")
            ], VirtualProjectDirectory: "src/App.Core"),
            new ProjectSpec("App.Core.Tests", [
                ("ServiceTests.cs", "public class ServiceTests {}")
            ], VirtualProjectDirectory: "tests/App.Core.Tests")
        );

        var coreProject = solution.Solution.Projects.First(p => p.Name == "App.Core");
        var testProject = solution.Solution.Projects.First(p => p.Name == "App.Core.Tests");

        Assert.False(TestDetector.IsTestProject(coreProject));
        Assert.True(TestDetector.IsTestProject(testProject));

        var preferred = TestDetector.FindPreferredTestProject(solution.Solution);
        Assert.NotNull(preferred);
        Assert.Equal("App.Core.Tests", preferred.Name);
    }

    [Theory]
    [InlineData("CalculatorTests", "Calculator", true)]
    [InlineData("CalculatorTest", "Calculator", true)]
    [InlineData("TestCalculator", "Calculator", true)]
    [InlineData("CalculatorSolutionAnalysisTests", "Calculator", true)]
    [InlineData("CalculatorCacheTests", "Calculator", true)]
    [InlineData("CalculatorBroadScopeTests", "Calculator", true)]
    [InlineData("UnrelatedTests", "Calculator", false)]
    [InlineData("CalculatorService", "Calculator", false)]
    public void MatchesTestClassName_ValidatesAffixesAndPrefixes(string testClass, string target, bool expected)
    {
        var result = TestDetector.MatchesTestClassName(testClass, target);
        Assert.Equal(expected, result);
    }
}
