using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCommentRemoverTests
{
    [Fact]
    public void RemoveDisableAllLines_RemovesExactLineWithLf()
    {
        const string source = """
            // ainetlinter-disable all
            namespace Test;

            """;

        var result = DisableAllCommentRemover.RemoveDisableAllLines(source);

        Assert.DoesNotContain("// ainetlinter-disable all", result);
        Assert.Contains("namespace Test;", result);
    }

    [Fact]
    public void RemoveDisableAllLines_RemovesExactLineWithCrLf()
    {
        const string source = "// ainetlinter-disable all\r\nnamespace Test;\r\n";

        var result = DisableAllCommentRemover.RemoveDisableAllLines(source);

        Assert.DoesNotContain("// ainetlinter-disable all", result);
        Assert.StartsWith("namespace Test;", result);
    }

    [Fact]
    public void RemoveDisableAllLines_KeepsIndentedOrPartialMatches()
    {
        const string source = """
             // ainetlinter-disable all
            // ainetlinter-disable all extra
            namespace Test;

            """;

        var result = DisableAllCommentRemover.RemoveDisableAllLines(source);

        Assert.Contains(" // ainetlinter-disable all", result);
        Assert.Contains("// ainetlinter-disable all extra", result);
    }

    [Fact]
    public void RemoveDisableAllLines_RemovesLineInMiddleOfFile()
    {
        const string source = """
            namespace Test
            {
            // ainetlinter-disable all
                public sealed class Example {}
            }

            """;

        var result = DisableAllCommentRemover.RemoveDisableAllLines(source);

        Assert.DoesNotContain("// ainetlinter-disable all", result);
        Assert.Contains("public sealed class Example", result);
    }

    [Fact]
    public void TryRemoveFromFile_RemovesOnlyExactDisableAllLine()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-remove-{Guid.NewGuid():N}.cs");
        const string source = "// ainetlinter-disable all\nnamespace Test;\n";
        try
        {
            File.WriteAllText(filePath, source);

            var modified = DisableAllCommentRemover.TryRemoveFromFile(filePath);

            Assert.True(modified);
            Assert.Equal("namespace Test;\n", File.ReadAllText(filePath));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
