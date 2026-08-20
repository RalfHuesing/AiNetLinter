#nullable enable

using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.IntegrationTests.Output;

[Trait("Category", "Integration")]
public sealed class DebtReportBuilderTests
{
    [Fact]
    public async Task BuildAsync_IncludesActiveSuppressionsSection()
    {
        using var tempDir = TestTempDirectory.Create("debt-report-");

        var file1Content = """
            // ainetlinter-disable EnforceSealedClasses
            public class A {}
            // ainetlinter-disable MaxLineCount
            """;

        var file2Content = """
            // ainetlinter-disable all
            public class B {}
            """;

        tempDir.CreateFile("File1.cs", file1Content);
        tempDir.CreateFile("File2.cs", file2Content);

        var report = await DebtReportBuilder.BuildAsync(tempDir.DirectoryPath, null);

        Assert.Contains("## active suppressions by file", report);
        Assert.Contains("File1.cs: EnforceSealedClasses (Zeile 1), MaxLineCount (Zeile 3)", report);
        Assert.Contains("File2.cs: all (Zeile 1)", report);
    }
}
