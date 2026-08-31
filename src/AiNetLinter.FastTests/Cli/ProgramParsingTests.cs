#nullable enable

using System.CommandLine;
using AiNetLinter.Cli;
using Xunit;

namespace AiNetLinter.FastTests.Cli;

// @covers LinterArgs

[Trait("Category", "Unit")]
public sealed class ProgramParsingTests
{
    [Fact]
    public void CliCommandBuilder_Parses_AgentRulesPath()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "--agent-rules-path", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.AgentRulesPath);
    }

    [Fact]
    public void CliCommandBuilder_Parses_AgentRulesPath_WithAlias()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "-arp", "my-rules-dir" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal("my-rules-dir", parsed.AgentRulesPath);
    }

    [Fact]
    public void CliCommandBuilder_Parses_SyncAgentRulesOnly()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "--sync-agent-rules-only" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.True(parsed.SyncAgentRulesOnly);
    }

    [Fact]
    public void CliCommandBuilder_Parses_SyncAgentRulesOnly_WithAlias()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--config", "rules.json", "--path", ".", "-saro" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.True(parsed.SyncAgentRulesOnly);
    }

    [Fact]
    public void CliCommandBuilder_Parses_ParentPid()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--parent-pid", "1234" });
        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(1234, parsed.ParentPid);
    }

    [Fact]
    public void CliCommandBuilder_Parses_ProjectTtl_AsDecimalInvariant()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--mcp-project-ttl-minutes", "0.05" });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(0.05m, parsed.McpProjectTtlMinutes);
    }

    [Fact]
    public void CliCommandBuilder_WithoutProjectFlags_RegistryDefaultsRemainEffective()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server" });
        var parsed = CliCommandBuilder.Parse(result, options);

        Assert.Null(parsed.McpProjectTtlMinutes);
        Assert.Null(parsed.McpMaxProjects);

        var args = new LinterArgs
        {
            TargetPath = string.Empty,
            McpServer = parsed.McpServer,
            McpProjectTtlMinutes = parsed.McpProjectTtlMinutes,
            McpMaxProjects = parsed.McpMaxProjects,
            Verbose = false,
        };
        Assert.Null(args.Validate());
    }

    [Fact]
    public void CliCommandBuilder_Parses_MaxProjects()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--mcp-max-projects", "7" });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(7, parsed.McpMaxProjects);
    }

    [Fact]
    public void CliCommandBuilder_ParsesExternalResourceOverrides()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[]
        {
            "--mcp-server",
            "--mcp-external-max-disk-bytes", "100",
            "--mcp-external-max-memory-bytes", "200",
            "--mcp-external-max-parallel-operations", "3",
            "--mcp-external-max-resident-resources", "5",
            "--mcp-external-idle-ttl-minutes", "0.5",
        });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.Equal(100, parsed.McpExternalMaxDiskBytes);
        Assert.Equal(200, parsed.McpExternalMaxMemoryBytes);
        Assert.Equal(3, parsed.McpExternalMaxParallelOperations);
        Assert.Equal(5, parsed.McpExternalMaxResidentResources);
        Assert.Equal(0.5m, parsed.McpExternalIdleTtlMinutes);
    }

    [Fact]
    public void CliCommandBuilder_Parses_DaemonStartAndIdleExit()
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--daemon-start", "--mcp-daemon-idle-exit-minutes", "0.25" });
        Assert.Empty(result.Errors);

        var parsed = CliCommandBuilder.Parse(result, options);
        Assert.True(parsed.DaemonStart);
        Assert.Equal(0.25m, parsed.McpDaemonIdleExitMinutes);

        var args = new LinterArgs
        {
            TargetPath = string.Empty,
            DaemonStart = parsed.DaemonStart,
            McpDaemonIdleExitMinutes = parsed.McpDaemonIdleExitMinutes,
            Verbose = false,
        };
        Assert.Null(args.Validate());
    }

    [Theory]
    [InlineData("beta", "beta")]
    [InlineData("Beta_2.prod", "beta_2.prod")]
    public void CliCommandBuilder_ParsesAndNormalizes_DaemonInstance(string instance, string normalizedInstance)
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--daemon-instance", instance });

        Assert.Empty(result.Errors);
        Assert.Equal(normalizedInstance, CliCommandBuilder.Parse(result, options).DaemonInstance);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2beta")]
    [InlineData("beta/one")]
    [InlineData("beta one")]
    [InlineData("betaä")]
    [InlineData("abcdefghijklmnopqrstuvwxyz1234567890x")]
    public void CliCommandBuilder_Rejects_InvalidDaemonInstance(string instance)
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", "--daemon-instance", instance });

        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void LinterArgs_RejectsDaemonInstanceOutsideDaemonModes()
    {
        var args = new LinterArgs
        {
            TargetPath = ".",
            DaemonInstance = "beta",
            Verbose = false,
        };

        Assert.Contains("--daemon-instance", args.Validate(), StringComparison.Ordinal);
        Assert.Contains("nur", args.Validate(), StringComparison.Ordinal);
    }

    [Fact]
    public void LinterArgs_RejectsNonPositiveDaemonIdleExit()
    {
        var args = new LinterArgs
        {
            TargetPath = string.Empty,
            DaemonStart = true,
            McpDaemonIdleExitMinutes = 0m,
            Verbose = false,
        };

        Assert.Contains("mcp-daemon-idle-exit-minutes", args.Validate());
    }

    [Fact]
    public void LinterArgs_RejectsNonPositiveExternalResourceOverrides()
    {
        var invalidArguments = new[]
        {
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalMaxDiskBytes = 0,
                Verbose = false,
            },
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalMaxMemoryBytes = 0,
                Verbose = false,
            },
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalMaxParallelOperations = 0,
                Verbose = false,
            },
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalMaxResidentResources = 0,
                Verbose = false,
            },
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalIdleTtlMinutes = 0,
                Verbose = false,
            },
            new LinterArgs
            {
                TargetPath = string.Empty,
                McpServer = true,
                McpExternalIdleTtlMinutes = 0.0000000001m,
                Verbose = false,
            },
        };

        Assert.All(invalidArguments, arguments => Assert.Contains("mcp-external", arguments.Validate()));
    }

    [Theory]
    [InlineData("--mcp-project-ttl-minutes", "abc")]
    [InlineData("--mcp-project-ttl-minutes", "1,5")]
    [InlineData("--mcp-max-projects", "vier")]
    [InlineData("--mcp-daemon-idle-exit-minutes", "abc")]
    [InlineData("--mcp-daemon-idle-exit-minutes", "1,5")]
    [InlineData("--mcp-external-max-memory-bytes", "abc")]
    [InlineData("--mcp-external-idle-ttl-minutes", "abc")]
    public void CliCommandBuilder_InvalidFlagValues_AreHardParseErrors(string flag, string value)
    {
        var (root, options) = CliCommandBuilder.Build();
        var result = root.Parse(new[] { "--mcp-server", flag, value });

        Assert.NotEmpty(result.Errors);
    }
}
