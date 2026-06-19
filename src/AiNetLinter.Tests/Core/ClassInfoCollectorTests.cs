#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class ClassInfoCollectorTests
{
    [Fact]
    public void ClassInfoCollector_Collects_ClassInfo()
    {
        var (tree, model) = TestHelper.ParseCode(@"
public class Foo
{
    public void Run() { }
}");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Metrics = new MetricsConfig { ExcludeSwitchDispatcherCases = false } },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        ClassInfoCollector.Collect(node, ctx);

        Assert.Single(ctx.Classes);
        var info = ctx.Classes.First();
        Assert.Equal("Foo", info.Name);
        Assert.False(info.IsPartial);
        Assert.False(info.IsStatic);
    }

    [Fact]
    public void ClassInfoCollector_Collects_RecordInfo()
    {
        var (tree, model) = TestHelper.ParseCode(@"
public record FooRecord(string Bar);");
        var ctx = TestHelper.CreateContext(
            config: TestHelper.CreateDefaultConfig() with { Metrics = new MetricsConfig { ExcludeSwitchDispatcherCases = false } },
            semanticModel: model
        );

        var node = tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>().First();
        ClassInfoCollector.Collect(node, ctx);

        Assert.Single(ctx.Classes);
        var info = ctx.Classes.First();
        Assert.Equal("FooRecord", info.Name);
    }
}
