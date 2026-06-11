using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class BaselineComparerTests
{
    [Fact]
    public void Compare_IdenticalChecksums_ReturnsNoChanges()
    {
        var stored = CreateBaseline(("src/A.cs", "abc123"));
        var current = new Dictionary<string, string> { ["src/A.cs"] = "abc123" };

        var result = BaselineComparer.Compare(stored, current);

        Assert.False(result.HasAnyChange);
        Assert.Empty(result.ChangedFiles);
        Assert.Empty(result.RemovedFiles);
    }

    [Fact]
    public void Compare_ChangedChecksum_MarksFileAsChanged()
    {
        var stored = CreateBaseline(("src/A.cs", "old"));
        var current = new Dictionary<string, string> { ["src/A.cs"] = "new" };

        var result = BaselineComparer.Compare(stored, current);

        Assert.True(result.HasAnyChange);
        Assert.Contains("src/A.cs", result.ChangedFiles);
    }

    [Fact]
    public void Compare_NewFile_MarksFileAsChanged()
    {
        var stored = CreateBaseline(("src/A.cs", "abc"));
        var current = new Dictionary<string, string>
        {
            ["src/A.cs"] = "abc",
            ["src/B.cs"] = "def",
        };

        var result = BaselineComparer.Compare(stored, current);

        Assert.True(result.HasAnyChange);
        Assert.Contains("src/B.cs", result.ChangedFiles);
    }

    [Fact]
    public void Compare_RemovedFile_MarksAsRemoved()
    {
        var stored = CreateBaseline(("src/A.cs", "abc"), ("src/B.cs", "def"));
        var current = new Dictionary<string, string> { ["src/A.cs"] = "abc" };

        var result = BaselineComparer.Compare(stored, current);

        Assert.True(result.HasAnyChange);
        Assert.Contains("src/B.cs", result.RemovedFiles);
    }

    private static BaselineFile CreateBaseline(params (string Path, string Checksum)[] files)
    {
        return new BaselineFile
        {
            Files = files.ToDictionary(static f => f.Path, static f => f.Checksum),
        };
    }
}
