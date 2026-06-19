#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class SealedClassCheckerTests
{
    [Fact]
    public void SealedClassChecker_Reports_NonSealedConcreteClass()
    {
        var (tree, model) = TestHelper.ParseCode("public class Foo { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSealedClasses = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        SealedClassChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("EnforceSealedClasses", ctx.Violations.First().RuleName);
    }

    [Fact]
    public void SealedClassChecker_NoViolation_ForSealedClass()
    {
        var (tree, model) = TestHelper.ParseCode("public sealed class Foo { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSealedClasses = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        SealedClassChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void SealedClassChecker_NoViolation_ForStaticClass()
    {
        var (tree, model) = TestHelper.ParseCode("public static class Foo { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSealedClasses = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        SealedClassChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void SealedClassChecker_NoViolation_ForUnsealedPartialClass_WhenAllowed()
    {
        var (tree, model) = TestHelper.ParseCode("public partial class Foo { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceSealedClasses = true, AllowUnsealedPartialClasses = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        SealedClassChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void SealedClassChecker_NoViolation_ForExemptSuffix()
    {
        var (tree, model) = TestHelper.ParseCode("public class FooBase { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with 
            { 
                Global = new GlobalConfig 
                { 
                    EnforceSealedClasses = true,
                    SealedClassExemptSuffixes = new[] { "Base" }
                } 
            },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();

        SealedClassChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }
}
