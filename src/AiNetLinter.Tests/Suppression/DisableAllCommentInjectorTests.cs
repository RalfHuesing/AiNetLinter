using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCommentInjectorTests
{
    [Fact]
    public void PrependDisableAll_AddsCommentAtTop()
    {
        const string source = "namespace Test;";

        var result = DisableAllCommentInjector.PrependDisableAll(source);

        Assert.StartsWith("// ainetlinter-disable all", result);
        Assert.Contains("namespace Test;", result);
    }

    [Fact]
    public void PrependDisableAll_PreservesUtf8Bom()
    {
        const string source = "\uFEFFnamespace Test;";

        var result = DisableAllCommentInjector.PrependDisableAll(source);

        Assert.StartsWith("\uFEFF// ainetlinter-disable all", result);
    }

    [Fact]
    public void TryInjectIntoFile_SkipsWhenDisableAllAlreadyPresent()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-inject-{Guid.NewGuid():N}.cs");
        const string source = """
            // ainetlinter-disable all
            namespace Test;
            """;
        try
        {
            File.WriteAllText(filePath, source);

            var modified = DisableAllCommentInjector.TryInjectIntoFile(filePath);

            Assert.False(modified);
            Assert.Equal(source, File.ReadAllText(filePath));
        }
        finally
        {
            DeleteIfExists(filePath);
        }
    }

    [Fact]
    public void TryInjectIntoFile_PrependsCommentWhenMissing()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-inject-{Guid.NewGuid():N}.cs");
        const string source = "namespace Test;";
        try
        {
            File.WriteAllText(filePath, source);

            var modified = DisableAllCommentInjector.TryInjectIntoFile(filePath);

            Assert.True(modified);
            Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(filePath));
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
