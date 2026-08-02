#nullable enable

using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp;

internal sealed record McpCodeGraphServerOptions
{
    public required SourceFileCatalog? Catalog { get; init; }

    public required ILintConsole Console { get; init; }

    public int MaxLineCount { get; init; } = 700;

    /// <summary>Vollstaendige Linter-Konfiguration aus <c>rules.json</c> via <c>--config</c>,
    /// sonst Default-<see cref="Config"/>.</summary>
    public required Config Config { get; init; }

    public static McpCodeGraphServerOptions From(
        SourceFileCatalog? catalog,
        ILintConsole? console = null,
        int maxLineCount = 700,
        Config? config = null)
    {
        return new McpCodeGraphServerOptions
        {
            Catalog = catalog,
            Console = console ?? LinterConsole.Instance,
            MaxLineCount = maxLineCount,
            Config = config ?? new Config { Global = new GlobalConfig(), Metrics = new MetricsConfig() },
        };
    }
}
