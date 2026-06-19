#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class NamespaceCouplingCheckerTests
{
    [Fact]
    public void NamespaceCouplingChecker_Reports_ForbiddenNamespaceDependency()
    {
        var (tree, model) = TestHelper.ParseCode(@"
namespace MyFeature.Domain
{
    public class Service
    {
        public void Run()
        {
            var x = new MyFeature.Infrastructure.Helper();
        }
    }
}
namespace MyFeature.Infrastructure
{
    public class Helper {}
}");
        var config = TestHelper.CreateDefaultConfig() with
        {
            ForbiddenNamespaceDependencies = new[]
            {
                new NamespaceRule { SourceNamespace = "MyFeature.Domain", TargetNamespace = "MyFeature.Infrastructure" }
            }
        };

        var ctx = TestHelper.CreateContext(config: config, semanticModel: model);
        ctx.CurrentNamespace = "MyFeature.Domain";

        var nodes = tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>().ToList();
        foreach (var node in nodes)
        {
            NamespaceCouplingChecker.CheckForbiddenSymbolNamespace(node, ctx);
        }

        Assert.Contains(ctx.Violations, v => v.RuleName == "ForbiddenNamespaceDependency");
    }
}
