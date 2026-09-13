#nullable enable

using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class HandoffCounterStoreTests
{
    [Fact]
    public void Next_FirstCall_InitializesFileAtomicallyWithA()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        var store = new HandoffCounterStore(filePath);

        var result = store.Next();

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Value);
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal(1, state.FormatVersion);
        Assert.Equal("a", state.LastIssued);
    }

    [Fact]
    public void Next_ConsecutiveCalls_AdvanceSequentiallyWithoutDebounce()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        var store = new HandoffCounterStore(filePath);

        Assert.Equal("a", store.Next().Value);
        Assert.Equal("b", store.Next().Value);
        Assert.Equal("c", store.Next().Value);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal("c", state.LastIssued);
    }

    [Fact]
    public void Next_ResumesFromExistingHighWaterMark()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");

        var initialState = new HandoffCounterState(1, "z");
        File.WriteAllText(filePath, JsonSerializer.Serialize(initialState));

        var store = new HandoffCounterStore(filePath);
        var result = store.Next();

        Assert.True(result.IsSuccess);
        Assert.Equal("0", result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("not-json")]
    [InlineData("{\"formatVersion\": 2, \"lastIssued\": \"a\"}")]
    [InlineData("{\"formatVersion\": 1, \"lastIssued\": \"\"}")]
    [InlineData("{\"formatVersion\": 1, \"lastIssued\": \"invalid!char\"}")]
    public void Next_CorruptedOrInvalidFile_ReturnsHandoffCounterUnavailableAndNeverResets(string corruptedContent)
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        File.WriteAllText(filePath, corruptedContent);

        var store = new HandoffCounterStore(filePath);
        var result = store.Next();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(LinterErrorCodes.HandoffCounterUnavailable, result.Error.Value.Code);

        // Bestätige, dass die Datei nicht stillschweigend mit "a" überschrieben wurde
        Assert.Equal(corruptedContent, File.ReadAllText(filePath));
    }

    [Fact]
    public async Task Next_ParallelRequests_ProduceUniqueCountersWithoutDuplicates()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        var store = new HandoffCounterStore(filePath);

        const int count = 50;
        var results = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(Enumerable.Range(0, count), async (_, _) =>
        {
            await Task.Yield();
            var result = store.Next();
            Assert.True(result.IsSuccess);
            results.Add(result.Value!);
        });

        Assert.Equal(count, results.Count);
        Assert.Equal(count, results.Distinct().Count());
    }
}
