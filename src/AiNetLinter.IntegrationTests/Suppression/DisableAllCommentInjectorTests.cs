#nullable enable

using System.IO;
using AiNetLinter.Suppression;
using Xunit;

namespace AiNetLinter.IntegrationTests.Suppression;

[Trait("Category", "Integration")]
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
        using var tempDir = TestTempDirectory.Create("ainetlinter-inject-");
        const string source = """
            // ainetlinter-disable all
            namespace Test;
            """;
        var filePath = tempDir.CreateFile("Test.cs", source);

        var modified = DisableAllCommentInjector.TryInjectIntoFile(filePath);

        Assert.False(modified);
        Assert.Equal(source, File.ReadAllText(filePath));
    }

    [Fact]
    public void TryInjectIntoFile_PrependsCommentWhenMissing()
    {
        using var tempDir = TestTempDirectory.Create("ainetlinter-inject-");
        const string source = "namespace Test;";
        var filePath = tempDir.CreateFile("Test.cs", source);

        var modified = DisableAllCommentInjector.TryInjectIntoFile(filePath);

        Assert.True(modified);
        Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(filePath));
    }
}
