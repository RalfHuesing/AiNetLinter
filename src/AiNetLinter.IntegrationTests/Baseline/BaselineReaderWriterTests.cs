#nullable enable

using System.Collections.Generic;
using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.IntegrationTests.Baseline;

[Trait("Category", "Integration")]
public sealed class BaselineReaderWriterTests
{
    [Fact]
    public void WriteAndRead_Roundtrip_PreservesChecksums()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-baseline-");
        var tempFile = tempDir.GetPath("baseline.json");

        var checksums = new Dictionary<string, string>
        {
            ["src/B.cs"] = "bbb",
            ["src/A.cs"] = "aaa",
        };

        BaselineWriter.Write(tempFile, checksums);
        var loaded = BaselineReader.Read(tempFile);

        Assert.Equal(1, loaded.Version);
        Assert.Equal("aaa", loaded.Files["src/A.cs"]);
        Assert.Equal("bbb", loaded.Files["src/B.cs"]);
    }

    [Fact]
    public void Write_SortsKeysDeterministically()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-baseline-");
        var tempFile = tempDir.GetPath("baseline.json");

        BaselineWriter.Write(tempFile, new Dictionary<string, string>
        {
            ["src/Z.cs"] = "z",
            ["src/A.cs"] = "a",
        });

        var json = File.ReadAllText(tempFile);
        var aIndex = json.IndexOf("src/A.cs", StringComparison.Ordinal);
        var zIndex = json.IndexOf("src/Z.cs", StringComparison.Ordinal);

        Assert.True(aIndex < zIndex);
    }
}
