#nullable enable

namespace AiNetLinter.Tests.Mcp;

/// <summary>
/// Konfiguration fuer die Retry-Schleife in <see cref="McpTestClient.ConnectAsync"/> bei
/// flakeanfaelligen Parallel-Init-Szenarien (siehe TD-019, 010-Volllauf-Beobachtung).
/// Default: 3 Retries mit 500ms/1000ms/2000ms Backoff (Faktor 2.0) = max ~3.5s
/// zusaetzliche Wartezeit pro Connect im Worst-Case.
/// </summary>
public sealed record McpTestClientRetryOptions(
    int MaxRetries = 3,
    int BaseDelayMs = 500,
    double BackoffFactor = 2.0);
