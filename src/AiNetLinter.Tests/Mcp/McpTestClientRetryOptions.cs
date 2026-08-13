#nullable enable

namespace AiNetLinter.Tests.Mcp;

public sealed record McpTestClientRetryOptions(
    int MaxRetries = 3,
    int BaseDelayMs = 500,
    double BackoffFactor = 2.0);
