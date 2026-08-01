#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Live-Integrationstests für alle 9 MCP-Tools direkt gegen das eigene Repository.
/// Ersetzt ad-hoc Python-Dogfooding-Skripte durch saubere, automatisierte C# xUnit-Tests.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpLiveRepositoryTests
{
    private static string GetRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AiNetLinter.slnx")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("AiNetLinter.slnx konnte im Elternverzeichnispfad nicht gefunden werden.");
    }

    [Fact]
    public async Task LiveDogfood_FindSymbol_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "find_symbol",
            new Dictionary<string, object?>
            {
                ["namePattern"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("LinterEngine", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_FindReferences_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "find_references",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetImpact_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "get_impact",
            new Dictionary<string, object?>
            {
                ["symbolIdentifier"] = "LinterEngine",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetTypeHierarchy_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "get_type_hierarchy",
            new Dictionary<string, object?>
            {
                ["typeIdentifier"] = "McpCodeGraphServer"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Basisklassen", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetFileSkeleton_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "get_file_skeleton",
            new Dictionary<string, object?>
            {
                ["filePath"] = "src/AiNetLinter/Program.cs"
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("Program", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetIndexScope_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync("get_index_scope");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains(".cs", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LiveDogfood_GetHotspots_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync("get_hotspots");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_GetViolations_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync("get_violations");

        Assert.NotNull(text);
        Assert.NotEmpty(text);
    }

    [Fact]
    public async Task LiveDogfood_SearchPattern_ReturnsResults()
    {
        var repoRoot = GetRepositoryRoot();
        await using var client = await McpTestClient.ConnectAsync(repoRoot);

        var text = await client.CallToolGetTextAsync(
            "search_pattern",
            new Dictionary<string, object?>
            {
                ["pattern"] = "AiNetLinter",
                ["maxResults"] = 5
            });

        Assert.NotNull(text);
        Assert.NotEmpty(text);
        Assert.Contains("AiNetLinter", text, StringComparison.OrdinalIgnoreCase);
    }
}
