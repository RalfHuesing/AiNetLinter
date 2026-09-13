#nullable enable

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class HandoffHandleRegistryTests
{
    private sealed class FailingCounterStore : IHandoffCounterStore
    {
        public Result<string> Next() =>
            Result<string>.Failure(
                LinterErrorCodes.HandoffCounterUnavailable,
                "Counter-Speicher absichtlich nicht verfügbar.");
    }

    [Fact]
    public void GetOrCreateOpaqueHandleForOutput_ReturnsConsistentHandlesAndBijection()
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        const string internalId1 = "i:0:targetA:contentA:M:Namespace.Class.MethodA";
        const string internalId2 = "i:0:targetB:contentB:M:Namespace.Class.MethodB";

        var handle1 = registry.GetOrCreateOpaqueHandleForOutput(internalId1);
        var handle1Again = registry.GetOrCreateOpaqueHandleForOutput(internalId1);
        var handle2 = registry.GetOrCreateOpaqueHandleForOutput(internalId2);

        Assert.True(handle1.IsSuccess);
        Assert.Equal("h:a", handle1.Value);
        Assert.Equal("h:a", handle1Again.Value);

        Assert.True(handle2.IsSuccess);
        Assert.Equal("h:b", handle2.Value);

        // Roundtrip prüfen
        var restored1 = registry.RestoreInternalHandoffForInput(handle1.Value!);
        var restored2 = registry.RestoreInternalHandoffForInput(handle2.Value!);

        Assert.True(restored1.IsSuccess);
        Assert.Equal(internalId1, restored1.Value);

        Assert.True(restored2.IsSuccess);
        Assert.Equal(internalId2, restored2.Value);
    }

    [Fact]
    public void GetOrCreateOpaqueHandleForOutput_RejectsEmptyInternalId()
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        var result = registry.GetOrCreateOpaqueHandleForOutput("");
        Assert.False(result.IsSuccess);
        Assert.Equal(LinterErrorCodes.InvalidArgument, result.Error!.Value.Code);
    }

    [Fact]
    public void GetOrCreateOpaqueHandleForOutput_FailsWhenCounterStoreFails()
    {
        var registry = new HandoffHandleRegistry(new FailingCounterStore());
        var result = registry.GetOrCreateOpaqueHandleForOutput("i:0:t:c:M:Foo");

        Assert.False(result.IsSuccess);
        Assert.Equal(LinterErrorCodes.HandoffCounterUnavailable, result.Error!.Value.Code);
    }

    [Fact]
    public void RestoreInternalHandoffForInput_UnknownHandle_ReturnsHandoffUnknownWithoutLeakingDetails()
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        var result = registry.RestoreInternalHandoffForInput("h:unknown99");

        Assert.False(result.IsSuccess);
        Assert.Equal(LinterErrorCodes.HandoffUnknown, result.Error!.Value.Code);
        Assert.Contains("h:unknown99", result.Error.Value.Message);
        Assert.Contains("find_symbol", result.Error.Value.Hint);
    }

    [Theory]
    [InlineData("h:")]
    [InlineData("h:!")]
    [InlineData("h:a b")]
    [InlineData("h:a-b")]
    [InlineData("h:ä")]
    [InlineData("H:a")]
    [InlineData("h:too:many")]
    public void RestoreInternalHandoffForInput_MalformedHandle_ReturnsInvalidHandoff(string malformedHandle)
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        var result = registry.RestoreInternalHandoffForInput(malformedHandle);

        Assert.False(result.IsSuccess);
        Assert.Equal(LinterErrorCodes.InvalidHandoff, result.Error!.Value.Code);
    }

    [Fact]
    public void GetOpaqueHandleForOutputOrThrow_CounterFailure_DoesNotLeakInternalId()
    {
        var registry = new HandoffHandleRegistry(new FailingCounterStore());
        const string internalId = "i:0:target-token:snapshot-token:M:Namespace.Type.Member";

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.GetOpaqueHandleForOutputOrThrow(internalId));

        Assert.Contains(LinterErrorCodes.HandoffCounterUnavailable, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(internalId, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("i:0:target-token", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"h:\folder\file.cs:10:5")]
    [InlineData("h:/folder/file.cs")]
    [InlineData(@"c:\workspace\src\app.cs:42")]
    public void RestoreInternalHandoffForInput_WindowsDrivePaths_PassThroughUnchanged(string drivePath)
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        var result = registry.RestoreInternalHandoffForInput(drivePath);

        Assert.True(result.IsSuccess);
        Assert.Equal(drivePath, result.Value);
    }

    [Theory]
    [InlineData("M:Namespace.Class.Method(System.Int32)")]
    [InlineData("Greeter")]
    [InlineData("Class.Method")]
    [InlineData("src/File.cs:10:5")]
    [InlineData("")]
    [InlineData(null)]
    public void RestoreInternalHandoffForInput_SemanticInputs_PassThroughUnchanged(string? semanticInput)
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        var result = registry.RestoreInternalHandoffForInput(semanticInput!);

        Assert.True(result.IsSuccess);
        Assert.Equal(semanticInput, result.Value);
    }

    [Fact]
    public async Task GetOrCreateOpaqueHandleForOutput_ParallelAccess_ProducesConsistentBijection()
    {
        using var temp = TestTempDirectory.Create("registry-");
        var store = new HandoffCounterStore(temp.GetPath("counter.json"));
        var registry = new HandoffHandleRegistry(store);

        const int uniqueKeys = 20;
        const int iterationsPerKey = 5;
        var collected = new ConcurrentBag<(string Key, string Handle)>();

        await Parallel.ForEachAsync(Enumerable.Range(0, uniqueKeys * iterationsPerKey), async (index, _) =>
        {
            await Task.Yield();
            var key = $"i:0:target:content:M:Method_{index % uniqueKeys}";
            var result = registry.GetOrCreateOpaqueHandleForOutput(key);
            Assert.True(result.IsSuccess);
            collected.Add((key, result.Value!));
        });

        // Alle Handles für denselben Key müssen identisch sein
        var grouped = collected.GroupBy(x => x.Key).ToList();
        Assert.Equal(uniqueKeys, grouped.Count);
        foreach (var group in grouped)
        {
            Assert.Single(group.Select(x => x.Handle).Distinct());
        }

        // Alle Handles müssen untereinander verschieden sein
        var allHandles = grouped.Select(g => g.First().Handle).ToList();
        Assert.Equal(uniqueKeys, allHandles.Distinct().Count());
        Assert.Equal(uniqueKeys, registry.Count);
    }
}
