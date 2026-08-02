using System.IO;
using System.Linq;
using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class SuppressionScannerTests
{
    [Fact]
    public void ScanFile_ParsesVariousSuppressionStyles()
    {
        var content = """
            // ainetlinter-disable EnforceSealedClasses
            public class TestClass {}

            int x = 0; // ainetlinter-disable MaxLineCount

            /* ainetlinter-disable all */
            
            @* ainetlinter-disable BlazorRequireCodeBehind *@
            
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, content);

            var entries = SuppressionScanner.ScanFile(tempFile);

            Assert.Equal(5, entries.Count);

            Assert.Equal("EnforceSealedClasses", entries[0].RuleName);
            Assert.Equal(1, entries[0].LineNumber);

            Assert.Equal("MaxLineCount", entries[1].RuleName);
            Assert.Equal(5, entries[1].LineNumber);

            Assert.Equal("all", entries[2].RuleName);
            Assert.Equal(7, entries[2].LineNumber);

            Assert.Equal("BlazorRequireCodeBehind", entries[3].RuleName);
            Assert.Equal(9, entries[3].LineNumber);

            // 5. ainetlinter-disable without rule defaults to all
            Assert.Equal("all", entries[4].RuleName);
            Assert.Equal(11, entries[4].LineNumber);
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
