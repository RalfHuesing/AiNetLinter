#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AiNetLinter.Observability;

internal static class McpLogRecordReader
{
    private const string ToolCallRecordType = "tool_call";
    private const string UnknownToolName = "<unknown>";
    private const string UnclassifiedErrorCode = "UNCLASSIFIED";
    private const string ExceptionErrorCode = "EXCEPTION";
    private const string LoadingResponseMarker = "Server laedt die Solution noch";
    private const string CompleteResponseMarker = "Diese Daten sind vollstaendig fuer den angefragten Scope";

    internal static McpLogReadResult Read(string logFile)
    {
        var calls = new List<McpLogCall>();
        var invalidLineDetails = new List<string>();
        var invalidLineCount = 0;
        var ignoredRecordCount = 0;

        using var reader = new StreamReader(new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!IsToolCallRecord(root))
                {
                    ignoredRecordCount++;
                    continue;
                }

                calls.Add(ParseCall(root, logFile, lineNumber));
            }
            catch (JsonException ex)
            {
                invalidLineCount++;
                invalidLineDetails.Add($"{logFile}:{lineNumber} - {ex.Message}");
            }
        }

        return new McpLogReadResult(calls, invalidLineCount, invalidLineDetails, ignoredRecordCount);
    }

    private static McpLogCall ParseCall(JsonElement root, string logFile, int lineNumber)
    {
        var response = GetString(root, "response");
        var errorMessage = GetString(root, "errorMessage");
        var isErrorResult = GetBoolean(root, "isErrorResult");
        var success = GetBoolean(root, "success", defaultValue: true);
        var responseTruncated = GetBoolean(root, "responseTruncated");

        return new McpLogCall(
            LogFile: logFile,
            LineNumber: lineNumber,
            ToolName: GetString(root, "toolName") ?? UnknownToolName,
            ProcessId: GetInt32(root, "processId"),
            InstanceId: GetString(root, "instanceId") ?? string.Empty,
            Timestamp: ParseTimestamp(GetString(root, "timestamp")),
            DurationMs: GetInt64(root, "durationMs"),
            IsErrorResult: isErrorResult,
            ErrorCode: DetermineErrorCode(response, errorMessage, isErrorResult, success),
            ResponseTruncated: responseTruncated,
            Completeness: DetermineCompleteness(response, responseTruncated),
            IsLoadingResponse: response?.Contains(LoadingResponseMarker, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static string? DetermineErrorCode(
        string? response,
        string? errorMessage,
        bool isErrorResult,
        bool success)
    {
        var code = ExtractErrorCode(response) ?? ExtractErrorCode(errorMessage);
        if (code is not null)
        {
            return code;
        }

        if (!success)
        {
            return ExceptionErrorCode;
        }

        return isErrorResult ? UnclassifiedErrorCode : null;
    }

    private static string? ExtractErrorCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        const string prefix = "[ERROR]:";
        var prefixIndex = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (prefixIndex < 0)
        {
            return null;
        }

        var codeStart = prefixIndex + prefix.Length;
        while (codeStart < text.Length && char.IsWhiteSpace(text[codeStart]))
        {
            codeStart++;
        }

        var codeEnd = text.IndexOf(':', codeStart);
        if (codeEnd <= codeStart)
        {
            return null;
        }

        var code = text[codeStart..codeEnd].Trim();
        return code.Length > 0 && code.All(character => char.IsUpper(character) || char.IsDigit(character) || character == '_')
            ? code
            : null;
    }

    private static McpLogCompletenessState DetermineCompleteness(string? response, bool responseTruncated)
    {
        if (responseTruncated || response?.Contains("truncated", StringComparison.OrdinalIgnoreCase) == true ||
            response?.Contains("trunkiert", StringComparison.OrdinalIgnoreCase) == true)
        {
            return McpLogCompletenessState.Truncated;
        }

        return response?.Contains(CompleteResponseMarker, StringComparison.OrdinalIgnoreCase) == true
            ? McpLogCompletenessState.Complete
            : McpLogCompletenessState.Unknown;
    }

    private static DateTimeOffset? ParseTimestamp(string? timestamp) =>
        DateTimeOffset.TryParse(
            timestamp,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    private static bool IsToolCallRecord(JsonElement root) =>
        string.Equals(GetString(root, "recordType"), ToolCallRecordType, StringComparison.Ordinal);

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int GetInt32(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : 0;

    private static long GetInt64(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value) ? value : 0;

    private static bool GetBoolean(JsonElement root, string propertyName, bool defaultValue = false)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue,
        };
    }
}
