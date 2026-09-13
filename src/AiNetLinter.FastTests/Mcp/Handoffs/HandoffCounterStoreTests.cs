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
    public void Next_FirstCall_InitializesFileAtomicallyWithBatchHighWaterMark()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        const int batchSize = 10;
        var store = new HandoffCounterStore(filePath, batchSize: batchSize);

        var result = store.Next();

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Value);
        Assert.Equal(batchSize - 1, store.BufferedCount);
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal(1, state.FormatVersion);

        // Bei batchSize=10: 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j' -> High-Water-Mark ist 'j'
        Assert.Equal("j", state.LastIssued);
    }

    [Fact]
    public void Next_ServesConsecutiveCallsFromBufferWithoutDiskWrites()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        const int batchSize = 5;
        var store = new HandoffCounterStore(filePath, batchSize: batchSize);

        Assert.Equal("a", store.Next().Value);
        var writeTimeAfterFirstCall = File.GetLastWriteTimeUtc(filePath);

        // Die nächsten 4 Aufrufe kommen direkt aus dem RAM-Puffer
        Assert.Equal("b", store.Next().Value);
        Assert.Equal("c", store.Next().Value);
        Assert.Equal("d", store.Next().Value);
        Assert.Equal("e", store.Next().Value);

        Assert.Equal(0, store.BufferedCount);
        Assert.Equal(writeTimeAfterFirstCall, File.GetLastWriteTimeUtc(filePath));

        // 6. Aufruf leert den Puffer und holt einen neuen Batzen von Disk ('f'..'j')
        Assert.Equal("f", store.Next().Value);
        Assert.Equal(batchSize - 1, store.BufferedCount);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal("j", state.LastIssued);
    }

    [Fact]
    public void Next_ResumesFromExistingHighWaterMark()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");

        var initialState = new HandoffCounterState(1, "z");
        File.WriteAllText(filePath, JsonSerializer.Serialize(initialState));

        var store = new HandoffCounterStore(filePath, batchSize: 3);
        var result = store.Next();

        Assert.True(result.IsSuccess);
        // Hinter 'z' kommen '0', '1', '2'
        Assert.Equal("0", result.Value);
        Assert.Equal(2, store.BufferedCount);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal("2", state.LastIssued);
    }

    [Fact]
    public void Next_WithBatchSize1_WorksSequentially()
    {
        using var temp = TestTempDirectory.Create("counter-store-");
        var filePath = temp.GetPath("handoff-counter.json");
        var store = new HandoffCounterStore(filePath, batchSize: 1);

        Assert.Equal("a", store.Next().Value);
        Assert.Equal("b", store.Next().Value);
        Assert.Equal("c", store.Next().Value);

        var json = File.ReadAllText(filePath);
        var state = JsonSerializer.Deserialize<HandoffCounterState>(json);
        Assert.NotNull(state);
        Assert.Equal("c", state.LastIssued);
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
        var store = new HandoffCounterStore(filePath, batchSize: 10);

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
