#nullable enable

using AiNetLinter.Mcp.Daemon;
using System.Text.Json;

namespace AiNetLinter.FastTests.Mcp.Daemon;

[Trait("Category", "Unit")]
public sealed class MruStateStoreTests
{
    [Fact]
    public async Task Roundtrip_IsSortedDeduplicatedAndBounded()
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var clock = new MruTestClock();
        var path = temp.GetPath("daemon-state.json");
        await using (var store = CreateStore(path, clock, maxProjects: 2))
        {
            store.Touch(Path.Combine(temp.DirectoryPath, "old"), clock.UtcDateTime.AddMinutes(-2));
            store.Touch(Path.Combine(temp.DirectoryPath, "new"), clock.UtcDateTime.AddMinutes(-1));
            store.Touch(Path.Combine(temp.DirectoryPath, "new"), clock.UtcDateTime);
            store.Touch(Path.Combine(temp.DirectoryPath, "third"), clock.UtcDateTime.AddSeconds(-30));
            await store.WriteIfDueAsync(force: true);
        }

        await using var reloaded = CreateStore(path, clock, maxProjects: 2);
        var entries = reloaded.Read(2);

        Assert.Equal(2, entries.Count);
        Assert.Equal(Path.Combine(temp.DirectoryPath, "new"), entries[0].RootPath);
        Assert.Equal(Path.Combine(temp.DirectoryPath, "third"), entries[1].RootPath);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    public async Task Read_EmptyOrCorruptFile_IsIgnored(string content)
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var path = temp.CreateFile("daemon-state.json", content);
        await using var store = CreateStore(path, new MruTestClock());

        Assert.Empty(store.Read(4));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    public async Task DisposeAfterEmptyOrCorruptRead_WritesValidEmptyArray(string content)
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var path = temp.CreateFile("daemon-state.json", content);
        var store = CreateStore(path, new MruTestClock());

        Assert.Empty(store.Read(4));
        await store.DisposeAsync();

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task ReadAndRemove_CanonicalizesAliasAndPersistsItsRemoval()
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var path = temp.GetPath("daemon-state.json");
        var root = Path.Combine(temp.DirectoryPath, "project");
        var alias = Path.Combine(root, ".");
        var json = JsonSerializer.Serialize(new[] { new MruStateEntry(alias, new DateTime(2026, 1, 1)) });
        File.WriteAllText(path, json);

        var store = CreateStore(path, new MruTestClock());
        var entries = store.Read(4);
        Assert.Equal(Path.GetFullPath(root), entries.Single().RootPath);

        store.Remove(alias);
        await store.DisposeAsync();

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));
        Assert.Empty(document.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Write_IsDebouncedAndRemoveUpdatesState()
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var clock = new MruTestClock();
        var path = temp.GetPath("daemon-state.json");
        var root = Path.Combine(temp.DirectoryPath, "project");
        await using var store = CreateStore(path, clock);

        store.Touch(root);
        await store.WriteIfDueAsync();
        Assert.False(File.Exists(path));

        clock.Advance(TimeSpan.FromSeconds(30));
        await store.WriteIfDueAsync();
        Assert.True(File.Exists(path));

        store.Remove(root);
        await store.WriteIfDueAsync(force: true);
        Assert.Empty(store.Read(4));
        Assert.DoesNotContain(".tmp-", string.Join(";", Directory.GetFiles(temp.DirectoryPath)));
    }

    [Fact]
    public async Task Write_UsesAtomicReplacementShape()
    {
        using var temp = TestTempDirectory.Create("daemon-mru-");
        var clock = new MruTestClock();
        var path = temp.GetPath("daemon-state.json");
        await using var store = CreateStore(path, clock);
        store.Touch(Path.Combine(temp.DirectoryPath, "project"));

        await store.WriteIfDueAsync(force: true);

        var parsed = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(path));
        Assert.Equal(JsonValueKind.Array, parsed.ValueKind);
    }

    private static MruStateStore CreateStore(string path, MruTestClock clock, int maxProjects = 4) =>
        new(new MruStateStoreOptions(path, clock, Debounce: TimeSpan.FromSeconds(30), MaxProjects: maxProjects));

    private sealed class MruTestClock : TimeProvider
    {
        private long ticks = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);

        public void Advance(TimeSpan delta) => Interlocked.Add(ref ticks, delta.Ticks);

        public DateTime UtcDateTime => GetUtcNow().UtcDateTime;
    }
}
