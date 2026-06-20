#nullable enable

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class LinqChainLengthCheckerTests
{
    // --- Positiv-Tests (Violation erwartet) ---

    [Fact]
    public void LinqChain_ExceedsLimit_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5).Skip(1);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 4), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("MaxLinqChainLength", ctx.Violations[0].RuleName);
    }

    [Fact]
    public void LinqChain_AtLimit_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 4), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Negativ-Tests ---

    [Fact]
    public void Disabled_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).Take(5).Skip(1);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 0), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void BuilderChain_NonLinqMethods_NoViolation()
    {
        // .AddLogging().AddRouting().Build() sind keine LINQ-Methoden → keine Violation
        var (tree, model) = TestHelper.ParseCode("""
            public class Builder
            {
                public Builder AddLogging() => this;
                public Builder AddRouting() => this;
                public Builder AddCaching() => this;
                public Builder AddAuth() => this;
                public Builder AddCors() => this;
                public void Build() { }
            }
            public class Foo
            {
                public void Run(Builder b)
                {
                    b.AddLogging().AddRouting().AddCaching().AddAuth().AddCors().Build();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 3), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void OnlyOneViolation_PerChain()
    {
        // Eine Kette aus 6 Methoden soll genau eine Violation erzeugen, nicht 6
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x)
                            .Take(5).Skip(1).Distinct();
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 3), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
    }

    [Fact]
    public void CustomLinqMethod_InWhitelist_Counts()
    {
        // Benutzerdefinierte Methode die zur Whitelist hinzugefügt wird
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public static class MyExtensions
            {
                public static IEnumerable<T> FilterActive<T>(this IEnumerable<T> src) => src;
            }
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Select(x => x * 2).OrderBy(x => x).FilterActive();
            }
            """);
        var customNames = new List<string>(DefaultLinqNames()) { "FilterActive" };
        var ctx = TestHelper.CreateContext(
            config: ConfigWithCustomNames(limit: 3, names: customNames),
            semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Single(ctx.Violations);
    }

    [Fact]
    public void ShortChain_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Collections.Generic;
            using System.Linq;
            public class Foo
            {
                public IEnumerable<int> Run(List<int> items)
                    => items.Where(x => x > 0).Take(5);
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(limit: 5), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            LinqChainLengthChecker.Check(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Hilfsmethoden ---

    private static LinterConfig ConfigWith(int limit) =>
        TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxLinqChainLength = limit }
        };

    private static LinterConfig ConfigWithCustomNames(int limit, IReadOnlyCollection<string> names) =>
        TestHelper.CreateDefaultConfig() with
        {
            Metrics = new MetricsConfig { MaxLinqChainLength = limit, LinqMethodNames = names }
        };

    private static IReadOnlyCollection<string> DefaultLinqNames() =>
        new MetricsConfig().LinqMethodNames;
}
