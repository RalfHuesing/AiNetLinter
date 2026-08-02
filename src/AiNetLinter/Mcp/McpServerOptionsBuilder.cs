#nullable enable

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp;

/// <summary>
/// Fluent-Builder fuer <see cref="McpServerOptions"/>. Aus <see cref="McpServerOptionsFactory"/>
/// ausgelagert, um die Factory selbst unter dem <c>AIContextFootprint</c>-Limit zu halten
/// (siehe <c>AiNetLinter.mdc</c> Z. 15, 28) und kuenftige P0/P1-Erweiterungen
/// (<c>--mcp-log</c>-State, <c>rules.json</c>-Auto-Discovery-Hint, "laedt noch"-State) als
/// additive <c>With*</c>-Methoden zu ermoeglichen, ohne die Factory selbst zu vergroessern.
/// Instanzen sind nicht thread-safe — pro Build-Vorgang neu erzeugen.
/// </summary>
internal sealed class McpServerOptionsBuilder
{
    private const string DefaultServerName = "ainetlinter";
    private const string FallbackVersion = "0.0.0";

    private string _serverName = DefaultServerName;
    private string? _serverVersion;
    private string _serverInstructions = string.Empty;
    private McpServerPrimitiveCollection<McpServerTool>? _toolCollection;

    public McpServerOptionsBuilder WithServerName(string name)
    {
        _serverName = name;
        return this;
    }

    public McpServerOptionsBuilder WithServerVersion(string? version)
    {
        _serverVersion = version;
        return this;
    }

    public McpServerOptionsBuilder WithServerInstructions(string instructions)
    {
        _serverInstructions = instructions;
        return this;
    }

    public McpServerOptionsBuilder WithToolCollection(McpServerPrimitiveCollection<McpServerTool> tools)
    {
        _toolCollection = tools;
        return this;
    }

    internal McpServerOptions Build()
    {
        return new McpServerOptions
        {
            ServerInfo = new Implementation
            {
                Name = _serverName,
                Version = _serverVersion ?? FallbackVersion,
            },
            ServerInstructions = _serverInstructions,
            ToolCollection = _toolCollection ?? new McpServerPrimitiveCollection<McpServerTool>(),
        };
    }
}
