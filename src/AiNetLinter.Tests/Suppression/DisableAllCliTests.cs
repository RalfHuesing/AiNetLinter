using System.Threading.Tasks;
using AiNetLinter.Suppression;
using AiNetLinter.Tests.Fixtures;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

public sealed class DisableAllCliTests
{
    [Fact]
    public async Task AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();

        var result = await CliProcessRunner.RunLinterAsync(
            $"--config \"{workspace.ConfigPath}\" --path \"{workspace.RootPath}\" --add-disable-all");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("OK", result.Output);
        Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(workspace.ViolatingClassPath));
    }

    [Fact]
    public async Task RemoveDisableAll_OnFixture_RemovesExactDisableAllLine()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var originalContent = File.ReadAllText(workspace.ViolatingClassPath);
        File.WriteAllText(workspace.ViolatingClassPath, DisableAllCommentInjector.PrependDisableAll(originalContent));

        var result = await CliProcessRunner.RunLinterAsync($"--path \"{workspace.RootPath}\" --remove-disable-all");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(originalContent, File.ReadAllText(workspace.ViolatingClassPath));
    }

    [Fact]
    public async Task Main_AddDisableAllWithBaseline_ReturnsExitCodeOne()
    {
        var exitCode = await AiNetLinter.Program.Main(new[]
        {
            "--path", ".",
            "--add-disable-all",
            "--baseline", "out.json",
        });

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public async Task Main_AddAndRemoveDisableAll_ReturnsExitCodeOne()
    {
        var exitCode = await AiNetLinter.Program.Main(new[]
        {
            "--path", ".",
            "--add-disable-all",
            "--remove-disable-all",
        });

        Assert.Equal(1, exitCode);
    }
}
