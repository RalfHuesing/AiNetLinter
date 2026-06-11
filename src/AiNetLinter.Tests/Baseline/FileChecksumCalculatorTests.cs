using AiNetLinter.Baseline;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

public sealed class FileChecksumCalculatorTests
{
    [Fact]
    public void ComputeSha256Hex_KnownContent_ReturnsExpectedHash()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"ainetlinter-checksum-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "hello");

            var hash = FileChecksumCalculator.ComputeSha256Hex(tempFile);

            Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
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
