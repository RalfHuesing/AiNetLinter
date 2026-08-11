#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Commands;
using AiNetLinter.Tests.Fixtures;
using AiNetLinter.Tests.Output;
using Xunit;

namespace AiNetLinter.Tests.Baseline;

[Trait("Category", "Integration")]
public sealed class WebBaselineTests
{
    [Fact]
    public async Task CreateBaseline_WithWebEnabled_IncludesWebFiles()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-web-baseline-{Guid.NewGuid():N}.json");

        try
        {
            // 1. Enable Web features in rules.json
            var rulesJson = File.ReadAllText(workspace.ConfigPath);
            rulesJson = rulesJson.Replace("\"IsEnabled\": false", "\"IsEnabled\": true");
            File.WriteAllText(workspace.ConfigPath, rulesJson);

            // 2. Create some web files in the workspace project directory
            var projectDir = Path.Combine(workspace.RootPath, "src", "BaselineMini");
            var cssPath = Path.Combine(projectDir, "styles.css");
            var razorPath = Path.Combine(projectDir, "Component.razor");

            File.WriteAllText(cssPath, "body { color: red; }");
            File.WriteAllText(razorPath, "<h3>Component</h3>");

            // 3. Create baseline in-process
            var createArgs = new LinterArgs
            {
                TargetPath = workspace.RootPath,
                Verbose = false,
                ConfigPath = workspace.ConfigPath,
                CreateBaselinePath = baselinePath,
            };
            var createConsole = new TestLintConsole();
            var createExitCode = await MaintenanceCommand.TryRunAsync(createArgs, default, createConsole);

            Assert.Equal(0, createExitCode);
            Assert.True(File.Exists(baselinePath));

            var baseline = BaselineReader.Read(baselinePath);

            // 4. Verify web files are in baseline files list
            var relativeCss = Path.GetRelativePath(workspace.RootPath, cssPath).Replace('\\', '/');
            var relativeRazor = Path.GetRelativePath(workspace.RootPath, razorPath).Replace('\\', '/');

            Assert.True(baseline.Files.ContainsKey(relativeCss), $"Baseline should contain CSS file: {relativeCss}");
            Assert.True(baseline.Files.ContainsKey(relativeRazor), $"Baseline should contain Razor file: {relativeRazor}");
        }
        finally
        {
            TestHelper.DeleteFileIfExists(baselinePath);
        }
    }

    [Fact]
    public async Task AuditWithBaseline_ChangedWebFile_ReportsViolationsAndUpdatesBaseline()
    {
        using var workspace = new BaselineMiniFixtureWorkspace();
        var baselinePath = Path.Combine(Path.GetTempPath(), $"ainetlinter-web-baseline-{Guid.NewGuid():N}.json");

        try
        {
            // 1. Enable Web features in rules.json and set MaxCssLineCount to a small value (e.g., 2)
            var rulesJson = File.ReadAllText(workspace.ConfigPath);
            rulesJson = rulesJson.Replace("\"IsEnabled\": false", "\"IsEnabled\": true");
            rulesJson = rulesJson.Replace("\"MaxCssLineCount\": 300", "\"MaxCssLineCount\": 2");
            File.WriteAllText(workspace.ConfigPath, rulesJson);

            // 2. Create web files in the workspace project directory
            var projectDir = Path.Combine(workspace.RootPath, "src", "BaselineMini");
            var cssPath = Path.Combine(projectDir, "styles.css");

            // CSS has 1 line initially (no violation)
            File.WriteAllText(cssPath, ".btn { color: blue; }");

            // 3. Create baseline in-process
            var createArgs = new LinterArgs
            {
                TargetPath = workspace.RootPath,
                Verbose = false,
                ConfigPath = workspace.ConfigPath,
                CreateBaselinePath = baselinePath,
            };
            var createExitCode = await MaintenanceCommand.TryRunAsync(createArgs, default, new TestLintConsole());
            Assert.Equal(0, createExitCode);

            // 4. Modify css file to violate the MaxCssLineCount rule (3 lines > 2 limit)
            File.WriteAllText(cssPath, ".btn {" + Environment.NewLine + "  color: blue;" + Environment.NewLine + "}");

            // 5. Audit - should report the CSS violation on the changed file
            var auditArgs = new LinterArgs
            {
                TargetPath = workspace.RootPath,
                Verbose = false,
                ConfigPath = workspace.ConfigPath,
                BaselinePath = baselinePath,
            };
            var auditConsole = new TestLintConsole();
            var auditExitCode = await AuditCommand.RunAsync(auditArgs, default, auditConsole);

            Assert.Equal(1, auditExitCode);
            Assert.Contains("CSS_MaxCssLineCount", auditConsole.OutputText);

            // 6. Run audit again - the updated baseline now includes the changed checksum, so
            //    the second audit reports no violations. This implicitly verifies that the
            //    baseline file was updated (no need to re-read the JSON for an explicit assert,
            //    the audit-result invariant is stronger).
            var secondAuditConsole = new TestLintConsole();
            var secondAuditExitCode = await AuditCommand.RunAsync(auditArgs, default, secondAuditConsole);
            Assert.Equal(0, secondAuditExitCode);
        }
        finally
        {
            TestHelper.DeleteFileIfExists(baselinePath);
        }
    }

}
