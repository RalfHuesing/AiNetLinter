#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class ValueObjectCheckerTests
{
    [Fact]
    public void ValueObjectChecker_Reports_ClassValueObject()
    {
        var (tree, model) = TestHelper.ParseCode("public class FooValueObject { }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceValueObjectContracts = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();

        ValueObjectChecker.Check(node, "FooValueObject", isRecord: false, ctx);

        Assert.Single(ctx.Violations);
        Assert.Contains("ist als 'class' deklariert", ctx.Violations.First().Details);
    }

    [Fact]
    public void ValueObjectChecker_Reports_MutableProperty()
    {
        var (tree, model) = TestHelper.ParseCode("public sealed record FooValueObject { public string Bar { get; set; } }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceValueObjectContracts = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();

        ValueObjectChecker.Check(node, "FooValueObject", isRecord: true, ctx);

        Assert.Single(ctx.Violations);
        Assert.Contains("enthaelt eine veraenderbare Eigenschaft", ctx.Violations.First().Details);
    }

    [Fact]
    public void ValueObjectChecker_NoViolation_ForReadOnlyStruct()
    {
        var (tree, model) = TestHelper.ParseCode("public readonly struct FooValueObject { public string Bar { get; } }");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { EnforceValueObjectContracts = true } },
            semanticModel: model
        );
        var node = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();

        ValueObjectChecker.Check(node, "FooValueObject", isRecord: false, ctx);

        Assert.Empty(ctx.Violations);
    }
}
