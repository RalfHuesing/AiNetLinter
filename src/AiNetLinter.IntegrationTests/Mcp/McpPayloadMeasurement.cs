#nullable enable

using System.Text;
using System.Text.Json;

namespace AiNetLinter.IntegrationTests.Mcp;

internal readonly record struct McpPayloadSize(int Characters, int Utf8Bytes)
{
    public override string ToString() => $"{Characters} Zeichen / {Utf8Bytes} UTF-8-Bytes";
}

internal static class McpPayloadMeasurement
{
    public static McpPayloadSize Measure(string payload) =>
        new(payload.Length, Encoding.UTF8.GetByteCount(payload));

    public static McpPayloadSize MeasureJson(JsonElement payload) =>
        Measure(JsonSerializer.Serialize(payload));
}
