#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class PhantomDependencyCheckerTests
{
    [Fact]
    public void PhantomDependencyChecker_Reports_ReflectionInvocation()
    {
        var (tree, model) = TestHelper.ParseCode(@"
using System;
public class Foo
{
    public void Load()
    {
        var type = Type.GetType(""SomeClass"");
    }
}");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Global = new GlobalConfig { DetectAndBanPhantomDependencies = true } },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        PhantomDependencyChecker.CheckPhantomReflection(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("DetectAndBanPhantomDependencies", ctx.Violations.First().RuleName);
    }
}
