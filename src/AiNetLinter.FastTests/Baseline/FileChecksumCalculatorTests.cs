using AiNetLinter.Baseline;
using Xunit;


namespace AiNetLinter.FastTests.Baseline;

[Trait("Category", "Unit")]
public sealed class FileChecksumCalculatorTests
{
    [Fact]
    public void ComputeSha256Hex_KnownContent_ReturnsExpectedHash()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-checksum-");
        var tempFile = tempDir.CreateFile("sample.txt", "hello");

        var hash = FileChecksumCalculator.ComputeSha256Hex(tempFile);

        Assert.Equal("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824", hash);
    }
}
