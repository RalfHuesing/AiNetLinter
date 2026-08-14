#nullable enable

using System.IO;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.FastTests.Core;

[Trait("Category", "Unit")]
public sealed class DiffImpactAnalyzerTests
{
    [Fact]
    public void ParseGitDiffHunks_WithValidDiff_ParsesHunksCorrectly()
    {
        const string diffOutput = """
            diff --git a/src/FileA.cs b/src/FileA.cs
            --- a/src/FileA.cs
            +++ b/src/FileA.cs
            @@ -12,3 +45,5 @@ public class FileA
            + added line 1
            + added line 2
            + added line 3
            diff --git a/src/FileB.cs b/src/FileB.cs
            --- a/src/FileB.cs
            +++ b/src/FileB.cs
            @@ -10 +20 @@ public class FileB
            """;

        var result = DiffImpactAnalyzer.ParseGitDiffHunks(diffOutput);

        var keyA = Path.Combine("src", "FileA.cs");
        var keyB = Path.Combine("src", "FileB.cs");

        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(keyA));
        Assert.True(result.ContainsKey(keyB));

        var fileALines = result[keyA];
        Assert.Equal(5, fileALines.Count);
        Assert.Contains(45, fileALines);
        Assert.Contains(49, fileALines);

        var fileBLines = result[keyB];
        Assert.Single(fileBLines);
        Assert.Contains(20, fileBLines);
    }
}
