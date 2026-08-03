#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Tests.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Deterministische Unit-Tests fuer die Retry-Schleife in
/// <see cref="McpTestClient.ConnectAsync"/>. Demonstriert den Failure-Mode ohne echten
/// Server-Subprozess — der FileNotFound-Pfad schlaegt deterministisch fehl, der Retry-Loop
/// wird sichtbar (A3 fuer.
/// </summary>
public sealed class McpTestClientRetryTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task ConnectAsync_AllRetriesExhausted_ThrowsInvalidOperationException()
    {
        // BaseDirectory enthaelt die AiNetLinter.exe (Test laeuft nach Build) — wir nutzen
        // einen leeren Zielpfad, der NICHT als gueltiges Verzeichnis existiert.
        var bogusDir = Path.Combine(Path.GetTempPath(), "mcp-test-nonexistent-" + Guid.NewGuid().ToString("N"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            McpTestClient.ConnectAsync(bogusDir, timeoutSeconds: 5,
                retryOptions: new McpTestClientRetryOptions(MaxRetries: 1, BaseDelayMs: 10, BackoffFactor: 1.0)));

        Assert.Contains("MCP-Client-Connect scheiterte", ex.Message);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void McpTestClientRetryOptions_DefaultValues_AreSane()
    {
        var options = new McpTestClientRetryOptions();

        Assert.Equal(3, options.MaxRetries);
        Assert.Equal(500, options.BaseDelayMs);
        Assert.Equal(2.0, options.BackoffFactor);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void McpTestClientRetryOptions_OverrideAllProperties()
    {
        var options = new McpTestClientRetryOptions(MaxRetries: 7, BaseDelayMs: 100, BackoffFactor: 1.5);

        Assert.Equal(7, options.MaxRetries);
        Assert.Equal(100, options.BaseDelayMs);
        Assert.Equal(1.5, options.BackoffFactor);
    }
}
