#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class AsyncVoidCheckerTests
{
    // --- Positiv-Tests (Violation erwartet) ---

    [Fact]
    public void AsyncVoidMethod_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class Foo
            {
                public async void Run() { }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(banAsyncVoid: true), semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanAsyncVoid", ctx.Violations[0].RuleName);
    }

    [Fact]
    public void AsyncVoidLocalFunction_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class Foo
            {
                public void Run()
                {
                    async void Inner() { }
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(banAsyncVoid: true), semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<LocalFunctionStatementSyntax>().First();

        AsyncVoidChecker.CheckLocalFunction(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanAsyncVoid", ctx.Violations[0].RuleName);
    }

    // --- Negativ-Tests (keine Violation erwartet) ---

    [Fact]
    public void AsyncTask_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task Run() { await Task.Delay(0); }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(banAsyncVoid: true), semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void EventHandler_AsyncVoid_Allowed_When_Flag_True()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System;
            public class Foo
            {
                public async void OnClick(object sender, EventArgs e) { }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(banAsyncVoid: true, allowEventHandlers: true),
            semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void EventHandler_AsyncVoid_Reported_When_Flag_False()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System;
            public class Foo
            {
                public async void OnClick(object sender, EventArgs e) { }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(banAsyncVoid: true, allowEventHandlers: false),
            semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Single(ctx.Violations);
    }

    [Fact]
    public void CustomEventArgs_AsyncVoid_Allowed()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System;
            public class ButtonClickedEventArgs : EventArgs { }
            public class Foo
            {
                public async void OnButtonClicked(object sender, ButtonClickedEventArgs e) { }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(banAsyncVoid: true, allowEventHandlers: true),
            semanticModel: model);
        var node = tree.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .First(m => m.Identifier.Text == "OnButtonClicked");

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void NonAsync_VoidMethod_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class Foo
            {
                public void Run() { }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(banAsyncVoid: true), semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void RuleDisabled_AsyncVoid_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class Foo
            {
                public async void Run() { }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(banAsyncVoid: false), semanticModel: model);
        var node = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().First();

        AsyncVoidChecker.CheckMethod(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Hilfsmethode ---

    private static LinterConfig ConfigWith(bool banAsyncVoid = true, bool allowEventHandlers = true) =>
        TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig
            {
                BanAsyncVoid = banAsyncVoid,
                AsyncVoidAllowEventHandlers = allowEventHandlers
            }
        };
}
