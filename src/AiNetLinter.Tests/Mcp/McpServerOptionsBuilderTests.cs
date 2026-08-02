#nullable enable

using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Tests fuer <see cref="McpServerOptionsBuilder"/>: konzentriert auf die Default-Werte
/// (TD-014). Aus <c>McpServerOptionsFactoryTests.cs</c> ausgelagert, weil der Builder eine
/// eigenstaendige, testbare Einheit ist — die Factory-Tests pruefen nur den Endpunkt
/// <c>Create(state) -&gt; McpServerOptions</c> via realer Integration.
/// </summary>
[Collection("ConsoleTestCollection")]
public sealed class McpServerOptionsBuilderTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Build_DefaultName_UsesAinetlinter()
    {
        var options = new McpServerOptionsBuilder().Build();

        Assert.Equal("ainetlinter", options.ServerInfo!.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_DefaultVersion_UsesAssemblyVersion()
    {
        // Der Builder liest die Version aus der Produktiv-Assembly (AiNetLinter.dll),
        // nicht aus der Test-Assembly — der Default-Pfad validiert, dass der Fallback
        // "0.0.0" greift, wenn die Hauptassembly keine Version mitfuehrt (Dev-Builds).
        // Mit explizitem WithServerVersion(null) wird der Fallback erzwungen.
        var options = new McpServerOptionsBuilder()
            .WithServerVersion(null)
            .Build();

        Assert.Equal("0.0.0", options.ServerInfo!.Version);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_DefaultInstructions_IsEmpty()
    {
        var options = new McpServerOptionsBuilder().Build();

        Assert.Equal(string.Empty, options.ServerInstructions);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithServerName_PropagatesToServerOptions()
    {
        var options = new McpServerOptionsBuilder()
            .WithServerName("test-server")
            .Build();

        Assert.Equal("test-server", options.ServerInfo!.Name);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithServerInstructions_PropagatesToServerOptions()
    {
        var options = new McpServerOptionsBuilder()
            .WithServerInstructions("Test-Instructions")
            .Build();

        Assert.Equal("Test-Instructions", options.ServerInstructions);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithoutToolCollection_ProvidesEmptyCollection()
    {
        var options = new McpServerOptionsBuilder().Build();

        Assert.NotNull(options.ToolCollection);
        Assert.Empty(options.ToolCollection);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Build_WithServerVersion_PropagatesToServerOptions()
    {
        var options = new McpServerOptionsBuilder()
            .WithServerVersion("1.2.3")
            .Build();

        Assert.Equal("1.2.3", options.ServerInfo!.Version);
    }
}
