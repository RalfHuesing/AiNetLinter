using Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Core;

namespace AiNetLinter.Tests;

public sealed class AnalyzerHelpersTests
{
    [Fact]
    public void IsTestFile_WithTestSuffix_ReturnsTrue()
    {
        Assert.True(AnalyzerHelpers.IsTestFile("MyClassTests.cs"));
        Assert.True(AnalyzerHelpers.IsTestFile("MyClassTest.cs"));
    }

    [Fact]
    public void IsTestFile_WithNormalFile_ReturnsFalse()
    {
        Assert.False(AnalyzerHelpers.IsTestFile("MyClass.cs"));
        Assert.False(AnalyzerHelpers.IsTestFile("Program.cs"));
    }

    [Fact]
    public void IsInPublicContext_WithPublicClass_ReturnsTrue()
    {
        var tree = CSharpSyntaxTree.ParseText(@"
public class MyPublicClass
{
    public void Run() {}
}");
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var methodDecl = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        Assert.True(AnalyzerHelpers.IsInPublicContext(classDecl));
        Assert.True(AnalyzerHelpers.IsInPublicContext(methodDecl));
    }

    [Fact]
    public void IsInPublicContext_WithPrivateClass_ReturnsFalse()
    {
        var tree = CSharpSyntaxTree.ParseText(@"
internal class MyPrivateClass
{
    public void Run() {}
}");
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var methodDecl = root.DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        Assert.False(AnalyzerHelpers.IsInPublicContext(classDecl));
        Assert.False(AnalyzerHelpers.IsInPublicContext(methodDecl));
    }
}
