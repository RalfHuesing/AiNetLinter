#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.CallTree;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.CallTree;

[Trait("Category", "Component")]
public sealed class GetCallTreeOutgoingCrossAssemblyTests
{
    [Fact]
    public async Task ExecuteAsync_OutgoingCrossAssemblyCall_RendersReferencedAssemblyPrefix()
    {
        using var temp = TestTempDirectory.Create("calltree-cross-asm-");
        var depCode = """
            namespace Vendor.Data;
            public interface IDataProvider
            {
                string GetData();
            }
            """;
        var depDllPath = AssemblyTestHelper.EmitAssembly(temp, "Vendor.Data", depCode);

        var consumerCode = """
            using Vendor.Data;
            namespace App;
            public class Consumer
            {
                public void DoWork(IDataProvider provider)
                {
                    provider.GetData();
                }
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\CallTreeTest.slnx",
            new ProjectSpec(
                "App",
                [("Consumer.cs", consumerCode)],
                AdditionalReferences: [MetadataReference.CreateFromFile(depDllPath)]));

        using var fixture = new McpInMemoryTestContext(testSolution);
        var server = fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            server,
            new GetCallTreeInput("Consumer.DoWork", 2, null, 10, Direction: "outgoing"),
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("[ref: Vendor.Data] IDataProvider.GetData", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OutgoingCall_ExcludesBclByDefault()
    {
        var consumerCode = """
            using System;
            namespace App;
            public class Consumer
            {
                public void Log()
                {
                    GC.Collect();
                }
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(consumerCode, "App", "Consumer.cs");
        using var fixture = new McpInMemoryTestContext(testSolution);
        var server = fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            server,
            new GetCallTreeInput("Consumer.Log", 2, null, 10, Direction: "outgoing", IncludeBcl: false),
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.DoesNotContain("GC.Collect", textContent.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("[ref:", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_OutgoingCall_IncludesBclWhenRequested()
    {
        var consumerCode = """
            using System;
            namespace App;
            public class Consumer
            {
                public void Log()
                {
                    GC.Collect();
                }
            }
            """;

        using var testSolution = RoslynTestSolutionFactory.CreateSolution(consumerCode, "App", "Consumer.cs");
        using var fixture = new McpInMemoryTestContext(testSolution);
        var server = fixture.CreateServer();

        var result = await GetCallTreeTool.ExecuteAsync(
            server,
            new GetCallTreeInput("Consumer.Log", 2, null, 10, Direction: "outgoing", IncludeBcl: true),
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("GC.Collect", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("[ref:", textContent.Text, StringComparison.Ordinal);
    }
}
