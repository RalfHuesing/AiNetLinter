using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Suppression;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;
using Xunit;

namespace AiNetLinter.Tests.Suppression;

[Trait("Category", "Integration")]
public sealed class DisableAllCliTests
{
    [Fact]
    public async Task AddDisableAll_OnViolatingFixture_InjectOnlyIntoViolatingFiles()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            ConfigPath = workspace.ConfigPath,
            AddDisableAll = true,
        };
        var console = new TestLintConsole();
        var exitCode = await MaintenanceCommand.TryRunAsync(args, default, console);

        Assert.Equal(0, exitCode);
        Assert.Contains("OK", console.OutputText);
        Assert.StartsWith("// ainetlinter-disable all", File.ReadAllText(workspace.ViolatingClassPath));
    }

    [Fact]
    public async Task RemoveDisableAll_OnFixture_RemovesExactDisableAllLine()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var originalContent = File.ReadAllText(workspace.ViolatingClassPath);
        File.WriteAllText(workspace.ViolatingClassPath, DisableAllCommentInjector.PrependDisableAll(originalContent));

        var args = new LinterArgs
        {
            TargetPath = workspace.RootPath,
            Verbose = false,
            RemoveDisableAll = true,
        };
        var console = new TestLintConsole();
        var exitCode = await MaintenanceCommand.TryRunAsync(args, default, console);

        Assert.Equal(0, exitCode);
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
