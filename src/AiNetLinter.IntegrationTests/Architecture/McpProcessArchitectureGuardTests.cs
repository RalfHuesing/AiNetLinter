#nullable enable

using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AiNetLinter.IntegrationTests.Architecture;

[Trait("Category", "Integration")]
public sealed class McpProcessArchitectureGuardTests
{
    [Fact]
    public void RunnerAndProcessCallsites_StayWithinMcpOwners()
    {
        var root = FindSolutionRoot();
        var runner = File.ReadAllText(Path.Combine(root, "src", "AiNetLinter.IntegrationTests", "xunit.runner.json"));

        Assert.Contains("\"parallelizeAssembly\": false", runner, StringComparison.Ordinal);
        Assert.Contains("\"parallelizeTestCollections\": true", runner, StringComparison.Ordinal);
        Assert.Contains("\"maxParallelThreads\": 4", runner, StringComparison.Ordinal);

        var mcpDirectory = Path.Combine(root, "src", "AiNetLinter.IntegrationTests", "Mcp");
        var sources = Directory.EnumerateFiles(mcpDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (Path: path, Text: File.ReadAllText(path)))
            .ToList();

        var transportCallsites = sources.Where(source => source.Text.Contains("new StdioClientTransport", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, transportCallsites.Count);
        Assert.All(transportCallsites, source => Assert.True(
            source.Path.EndsWith(Path.Combine("Mcp", "Platform", "McpProcessHost.cs"), StringComparison.Ordinal) ||
            source.Path.EndsWith(Path.Combine("Mcp", "McpHandshakeToolRegistrationTests.cs"), StringComparison.Ordinal) ||
            source.Path.EndsWith(Path.Combine("Mcp", "McpServerCommandErrorHandlingTests.cs"), StringComparison.Ordinal),
            $"Nicht besitzende StdioClientTransport-Callsite: {source.Path}"));

        var processCallsites = sources.Where(source => source.Text.Contains("Process.Start(", StringComparison.Ordinal)).ToList();
        Assert.Single(processCallsites);
        Assert.EndsWith(Path.Combine("Mcp", "McpServerCommandJsonRpcFramingTests.cs"), processCallsites[0].Path, StringComparison.Ordinal);
        Assert.Contains(sources, source =>
            source.Path.EndsWith(Path.Combine("Mcp", "McpProcessRunner.cs"), StringComparison.Ordinal) &&
            source.Text.Contains("process.Start();", StringComparison.Ordinal));
        Assert.DoesNotContain(sources, source => source.Text.Contains("SymbolGraphMcp", StringComparison.Ordinal));
    }

    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AiNetLinter.slnx"))) return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Das Root-Verzeichnis mit AiNetLinter.slnx wurde nicht gefunden.");
    }
}
