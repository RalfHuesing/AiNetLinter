#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class DynamicTypeCheckerTests
{
    [Fact]
    public void DynamicTypeChecker_Reports_DynamicUsage()
    {
        var (tree, model) = TestHelper.ParseCode(@"
public class Foo
{
    public void Run(dynamic x) { }
}");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { AllowDynamic = false } },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>()
            .First(n => n.Identifier.Text == "dynamic");

        DynamicTypeChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("AllowDynamic", ctx.Violations.First().RuleName);
    }
}
