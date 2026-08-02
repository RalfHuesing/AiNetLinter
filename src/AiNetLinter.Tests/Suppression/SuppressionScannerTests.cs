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

            // Some other comment
            int x = 0; // ainetlinter-disable MaxLineCount

            /* ainetlinter-disable all */
            
            @* ainetlinter-disable BlazorRequireCodeBehind *@
            
            // ainetlinter-disable
            """;

        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, content);

            var entries = SuppressionScanner.ScanFile(tempFile);

            Assert.Equal(5, entries.Count);

            // 1. EnforceSealedClasses
            Assert.Equal("EnforceSealedClasses", entries[0].RuleName);
            Assert.Equal(1, entries[0].LineNumber);

            // 2. MaxLineCount
            Assert.Equal("MaxLineCount", entries[1].RuleName);
            Assert.Equal(5, entries[1].LineNumber);

            // 3. all
            Assert.Equal("all", entries[2].RuleName);
            Assert.Equal(7, entries[2].LineNumber);

            // 4. BlazorRequireCodeBehind
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
