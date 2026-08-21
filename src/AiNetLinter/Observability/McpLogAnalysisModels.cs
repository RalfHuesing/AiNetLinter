#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Observability;

internal sealed record McpLogAnalysisReport(
    int SchemaVersion,
    string InputPath,
    IReadOnlyList<string> LogFiles,
    int ToolCallCount,
    int InvalidLineCount,
    IReadOnlyList<string> InvalidLineDetails,
    int IgnoredRecordCount,
    IReadOnlyDictionary<string, int> CallsPerTool,
    int ErrorResultCount,
    double IsErrorRate,
    IReadOnlyDictionary<string, int> ErrorCodes,
    int LoadingRetryBurstCount,
    int LoadingRetryCallCount,
    int ResponseTruncatedCount,
    int CompletenessCompleteCount,
    int CompletenessTruncatedCount,
    int CompletenessUnknownCount,
    string CompletenessDetection,
    IReadOnlyList<McpLogSessionSummary> Sessions);

internal sealed record McpLogSessionSummary(
    string LogFile,
    int ProcessId,
    string InstanceId,
    int ToolCallCount,
    string? FirstCallUtc,
    string? LastCallUtc,
    double DurationSeconds,
    long TotalDurationMs,
    IReadOnlyList<string> ToolSequence);

internal sealed record McpLogCall(
    string LogFile,
    int LineNumber,
    string ToolName,
    int ProcessId,
    string InstanceId,
    DateTimeOffset? Timestamp,
    long DurationMs,
    bool IsErrorResult,
    string? ErrorCode,
    bool ResponseTruncated,
    McpLogCompletenessState Completeness,
    bool IsLoadingResponse);

internal sealed record McpLogReadResult(
    IReadOnlyList<McpLogCall> Calls,
    int InvalidLineCount,
    IReadOnlyList<string> InvalidLineDetails,
    int IgnoredRecordCount);

internal enum McpLogCompletenessState
{
    Unknown,
    Complete,
    Truncated,
}
