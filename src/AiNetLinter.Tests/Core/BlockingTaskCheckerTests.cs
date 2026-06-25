#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core.Checkers;

namespace AiNetLinter.Tests.Core;

public sealed class BlockingTaskCheckerTests
{
    // --- .Wait() ---

    [Fact]
    public void TaskWait_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public void Run()
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- .Result ---

    [Fact]
    public void TaskResult_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public int Run()
                {
                    return Task.FromResult(42).Result;
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            BlockingTaskChecker.CheckMemberAccess(node, ctx);

        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- .GetAwaiter().GetResult() ---

    [Fact]
    public void GetAwaiterGetResult_Reports_Violation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public int Run()
                {
                    return Task.FromResult(42).GetAwaiter().GetResult();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        // Nur eine Violation: die äußerste GetResult()-Invocation
        Assert.Single(ctx.Violations);
        Assert.Equal("BanBlockingTaskAccess", ctx.Violations[0].RuleName);
    }

    // --- Negativ-Tests ---

    [Fact]
    public void AwaitedTask_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public async Task Run()
                {
                    await Task.Delay(100);
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void CustomClass_Result_Property_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            public class MyResult { public int Result { get; set; } }
            public class Foo
            {
                public int Run()
                {
                    var r = new MyResult();
                    return r.Result;
                }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: true), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<MemberAccessExpressionSyntax>())
            BlockingTaskChecker.CheckMemberAccess(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void StaticMain_Wait_Allowed_When_Flag_True()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Program
            {
                static void Main(string[] args)
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(ban: true, allowInMain: true),
            semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void RuleDisabled_NoViolation()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class Foo
            {
                public void Run() { Task.Delay(100).Wait(); }
            }
            """);
        var ctx = TestHelper.CreateContext(config: ConfigWith(ban: false), semanticModel: model);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    [Fact]
    public void TestFile_Wait_Allowed_When_Flag_True()
    {
        var (tree, model) = TestHelper.ParseCode("""
            using System.Threading.Tasks;
            public class FooTests
            {
                public void Setup()
                {
                    Task.Delay(100).Wait();
                }
            }
            """);
        var ctx = TestHelper.CreateContext(
            config: ConfigWith(ban: true, allowInTests: true),
            semanticModel: model,
            isTestFile: true);
        foreach (var node in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            BlockingTaskChecker.CheckInvocation(node, ctx);

        Assert.Empty(ctx.Violations);
    }

    // --- Hilfsmethode ---

    private static Config ConfigWith(
        bool ban = true,
        bool allowInMain = true,
        bool allowInTests = false) =>
        TestHelper.CreateDefaultConfig() with
        {
            Global = new GlobalConfig
            {
                BanBlockingTaskAccess = ban,
                BanBlockingTaskAccessAllowInMain = allowInMain,
                BanBlockingTaskAccessAllowInTests = allowInTests
            }
        };
}
