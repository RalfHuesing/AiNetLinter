using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class BaselineReaderWriterTests
{
    [Fact]
    public void WriteAndRead_Roundtrip_PreservesChecksums()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        try
        {
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
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Write_SortsKeysDeterministically()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ainetlinter-baseline-{Guid.NewGuid():N}.json");
        try
        {
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
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
