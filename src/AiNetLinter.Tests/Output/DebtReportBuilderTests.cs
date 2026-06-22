using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.Tests.Output;

public sealed class DebtReportBuilderTests
{
    [Fact]
    public async Task BuildAsync_IncludesActiveSuppressionsSection()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var file1 = Path.Combine(tempDir, "File1.cs");
        var file2 = Path.Combine(tempDir, "File2.cs");

        var file1Content = """
            // ainetlinter-disable EnforceSealedClasses
            public class A {}
            // ainetlinter-disable MaxLineCount
            """;

        var file2Content = """
            // ainetlinter-disable all
            public class B {}
            """;

        try
        {
            await File.WriteAllTextAsync(file1, file1Content);
            await File.WriteAllTextAsync(file2, file2Content);

            var report = await DebtReportBuilder.BuildAsync(tempDir, null);

            Assert.Contains("## active suppressions by file", report);
            Assert.Contains("File1.cs: EnforceSealedClasses (Zeile 1), MaxLineCount (Zeile 3)", report);
            Assert.Contains("File2.cs: all (Zeile 1)", report);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}
