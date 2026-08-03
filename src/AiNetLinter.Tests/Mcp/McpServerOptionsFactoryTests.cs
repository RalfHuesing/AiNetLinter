#nullable enable

using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Tests fuer <see cref="McpServerOptionsFactory"/>: konzentriert auf den zentralen Scope-Hint
///, der via <c>McpServerOptions.ServerInstructions</c> in der
/// <c>initialize</c>-Antwort des Servers landet. Aus <c>McpServerCommandTests.cs</c> ausgelagert,
/// weil diese Datei bereits am <c>MaxLineCount</c>-Limit (500) liegt und das Hinzufuegen
/// weiterer Tests dort <c>CliIntegrationTests</c> brechen wuerde (siehe Plan-Abweichung im
/// <c>result.md</c> von.
/// </summary>
public sealed class McpServerOptionsFactoryTests
{
    [Fact]
    public void Create_ServerInstructionsContainsScopeHint()
    {
        var state = new McpCodeGraphServer(McpCodeGraphServerOptions.From(null));
        var options = McpServerOptionsFactory.Create(state);

        Assert.False(string.IsNullOrEmpty(options.ServerInstructions));
        Assert.Contains(".cs", options.ServerInstructions);
        Assert.Contains("search_pattern", options.ServerInstructions);
        Assert.Contains(".js", options.ServerInstructions);
        Assert.Contains(".xaml", options.ServerInstructions);
    }
}
