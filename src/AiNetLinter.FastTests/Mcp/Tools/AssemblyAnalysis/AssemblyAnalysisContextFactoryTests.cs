#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisContextFactoryTests
{
    [Fact]
    public async Task CreateAsync_DecompilesAssemblyAndProducesValidContext()
    {
        using var temp = TestTempDirectory.Create("assembly-context-decompiled-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { public void Hello() { } }");

        var result = await AssemblyAnalysisContextFactory.CreateAsync(
            assemblyPath,
            consumerSolution: null,
            receiverType: null,
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Context);
        var context = result.Context!;

        Assert.Equal("TargetAssembly", context.Identity?.Name);
        Assert.NotNull(context.Compilation.GetTypeByMetadataName("Target.TargetOnly"));
        Assert.Equal("decompiled", context.Origin.OriginKind);
        Assert.True(context.Origin.IsDecompiled);
        Assert.Equal("available", context.Origin.BodyAvailability);
        Assert.Equal("decompiledProject", context.Origin.ContentMode);
        Assert.Equal(1, context.Generation);
        Assert.Equal(AssemblySessionStatus.Complete, context.Status);
        Assert.Empty(context.Diagnostics);
    }

    [Fact]
    public async Task CreateAsync_WithConsumerSolution_ResolvesReceiver()
    {
        using var temp = TestTempDirectory.Create("assembly-context-consumer-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");

        using var consumerSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("ConsumerProject", [("Receiver.cs", "namespace Consumer; public sealed class MyReceiver { }")]));

        var result = await AssemblyAnalysisContextFactory.CreateAsync(
            assemblyPath,
            consumerSolution.Solution,
            "Consumer.MyReceiver",
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Context);
        var context = result.Context!;

        Assert.NotNull(context.Receiver);
        Assert.Equal("MyReceiver", context.Receiver!.Name);
        Assert.Equal("ConsumerProject", context.ConsumerProject);
    }

    [Fact]
    public async Task CreateAsync_WithUnresolvableReceiver_AppendsDiagnostic()
    {
        using var temp = TestTempDirectory.Create("assembly-context-missing-receiver-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");

        using var consumerSolution = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("ConsumerProject", [("Receiver.cs", "namespace Consumer; public sealed class MyReceiver { }")]));

        var result = await AssemblyAnalysisContextFactory.CreateAsync(
            assemblyPath,
            consumerSolution.Solution,
            "Consumer.NonExistentReceiver",
            CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Context);
        var context = result.Context!;

        Assert.Null(context.Receiver);
        Assert.Contains(context.Diagnostics, d => d.Contains("Consumer.NonExistentReceiver", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateAsync_ThrowsOnNullOrWhitespacePath()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            AssemblyAnalysisContextFactory.CreateAsync("", null, null, CancellationToken.None));

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            AssemblyAnalysisContextFactory.CreateAsync(null!, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_NonExistentAssembly_ReturnsError()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "Missing.dll");

        var result = await AssemblyAnalysisContextFactory.CreateAsync(
            nonExistentPath,
            null,
            null,
            CancellationToken.None);

        Assert.Null(result.Context);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task FromGeneration_ConstructsContextWithCorrectProperties()
    {
        using var temp = TestTempDirectory.Create("assembly-from-generation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "GenAssembly",
            "namespace Gen; public sealed class GenClass { }");

        await using var session = new AssemblyAnalysisSession(assemblyPath);
        var refresh = await session.RefreshAsync(CancellationToken.None);
        Assert.Equal(AssemblySessionStatus.Complete, refresh.Status);

        Assert.NotNull(session.CurrentGeneration);
        var generation = session.CurrentGeneration!;
        var context = AssemblyAnalysisContextFactory.FromGeneration(generation);

        Assert.NotNull(context);
        Assert.Equal("GenAssembly", context.Identity?.Name);
        Assert.Equal(1, context.Generation);
        Assert.Equal("decompiled", context.Origin.OriginKind);
        Assert.True(context.Origin.IsDecompiled);
        Assert.Equal("available", context.Origin.BodyAvailability);
        Assert.Equal("decompiledProject", context.Origin.ContentMode);
    }
}
