#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AiNetLinter.Observability;

internal static class McpLogAnalyzer
{
    private const int LoadingRetryWindowSeconds = 5;
    private const int MinimumCallsPerLoadingBurst = 2;
    private const int ReportSchemaVersion = 1;
    private const string CompletenessDetectionMethod = "response-text-marker-heuristic";

    internal static McpLogAnalysisReport Analyze(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Ein Log-Pfad oder Log-Verzeichnis muss angegeben werden.", nameof(inputPath));
        }

        var logFiles = McpLogFileDiscovery.Discover(inputPath);
        var readResults = logFiles.Select(McpLogRecordReader.Read).ToArray();
        var orderedCalls = readResults
            .SelectMany(result => result.Calls)
            .OrderBy(call => call.Timestamp ?? DateTimeOffset.MaxValue)
            .ThenBy(call => call.LogFile, StringComparer.OrdinalIgnoreCase)
            .ThenBy(call => call.LineNumber)
            .ToArray();

        return BuildReport(inputPath, logFiles, orderedCalls, readResults);
    }

    internal static McpLogAnalysisResult TryAnalyze(string inputPath)
    {
        try
        {
            return new McpLogAnalysisResult(Analyze(inputPath), null);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new McpLogAnalysisResult(null, ex.Message);
        }
    }

    private static McpLogAnalysisReport BuildReport(
        string inputPath,
        IReadOnlyList<string> logFiles,
        IReadOnlyList<McpLogCall> calls,
        IReadOnlyList<McpLogReadResult> readResults)
    {
        var errorResultCount = calls.Count(call => call.IsErrorResult);
        var completenessCounts = CountCompleteness(calls);
        var (loadingBursts, loadingCalls) = CountLoadingBursts(calls);

        return new McpLogAnalysisReport(
            SchemaVersion: ReportSchemaVersion,
            InputPath: inputPath,
            LogFiles: logFiles,
            ToolCallCount: calls.Count,
            InvalidLineCount: readResults.Sum(result => result.InvalidLineCount),
            InvalidLineDetails: readResults
                .SelectMany(result => result.InvalidLineDetails)
                .OrderBy(detail => detail, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            IgnoredRecordCount: readResults.Sum(result => result.IgnoredRecordCount),
            CallsPerTool: CountBy(calls, call => call.ToolName),
            ErrorResultCount: errorResultCount,
            IsErrorRate: calls.Count == 0 ? 0 : (double)errorResultCount / calls.Count,
            ErrorCodes: CountBy(calls.Where(call => call.ErrorCode is not null), call => call.ErrorCode!),
            LoadingRetryBurstCount: loadingBursts,
            LoadingRetryCallCount: loadingCalls,
            ResponseTruncatedCount: calls.Count(call => call.ResponseTruncated),
            CompletenessCompleteCount: completenessCounts.Complete,
            CompletenessTruncatedCount: completenessCounts.Truncated,
            CompletenessUnknownCount: completenessCounts.Unknown,
            CompletenessDetection: CompletenessDetectionMethod,
            Sessions: BuildSessions(calls));
    }

    private static IReadOnlyList<McpLogSessionSummary> BuildSessions(IReadOnlyList<McpLogCall> calls)
    {
        return calls
            .GroupBy(call => call.LogFile, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sessionCalls = group
                    .OrderBy(call => call.Timestamp ?? DateTimeOffset.MaxValue)
                    .ThenBy(call => call.LineNumber)
                    .ToArray();
                var firstTimestamp = sessionCalls.Select(call => call.Timestamp).FirstOrDefault(timestamp => timestamp.HasValue);
                var lastTimestamp = sessionCalls.Select(call => call.Timestamp).LastOrDefault(timestamp => timestamp.HasValue);
                var durationSeconds = firstTimestamp.HasValue && lastTimestamp.HasValue
                    ? Math.Max(0, (lastTimestamp.Value - firstTimestamp.Value).TotalSeconds)
                    : 0;
                var firstCall = sessionCalls[0];
                return new McpLogSessionSummary(
                    LogFile: group.Key,
                    ProcessId: firstCall.ProcessId,
                    InstanceId: firstCall.InstanceId,
                    ToolCallCount: sessionCalls.Length,
                    FirstCallUtc: firstTimestamp?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    LastCallUtc: lastTimestamp?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                    DurationSeconds: durationSeconds,
                    TotalDurationMs: sessionCalls.Sum(call => call.DurationMs),
                    ToolSequence: sessionCalls.Select(call => call.ToolName).ToArray());
            })
            .ToArray();
    }

    private static (int BurstCount, int CallCount) CountLoadingBursts(IReadOnlyList<McpLogCall> calls)
    {
        var burstCount = 0;
        var burstCallCount = 0;

        foreach (var session in calls
            .GroupBy(call => call.LogFile, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderBy(call => call.Timestamp ?? DateTimeOffset.MaxValue)
                .ThenBy(call => call.LineNumber)))
        {
            var currentBurstLength = 0;
            McpLogCall? previous = null;
            foreach (var call in session)
            {
                var continuesBurst = IsLoadingBurstContinuation(previous, call);

                if (continuesBurst)
                {
                    currentBurstLength++;
                }
                else
                {
                    AddBurst(currentBurstLength, ref burstCount, ref burstCallCount);
                    currentBurstLength = call.IsLoadingResponse ? 1 : 0;
                }

                previous = call;
            }

            AddBurst(currentBurstLength, ref burstCount, ref burstCallCount);
        }

        return (burstCount, burstCallCount);
    }

    private static bool IsLoadingBurstContinuation(McpLogCall? previous, McpLogCall current)
    {
        if (previous is null || !previous.IsLoadingResponse || !current.IsLoadingResponse ||
            !string.Equals(previous.ToolName, current.ToolName, StringComparison.Ordinal) ||
            !previous.Timestamp.HasValue || !current.Timestamp.HasValue)
        {
            return false;
        }

        return current.Timestamp.Value >= previous.Timestamp.Value &&
            current.Timestamp.Value - previous.Timestamp.Value <= TimeSpan.FromSeconds(LoadingRetryWindowSeconds);
    }

    private static void AddBurst(int length, ref int burstCount, ref int callCount)
    {
        if (length < MinimumCallsPerLoadingBurst)
        {
            return;
        }

        burstCount++;
        callCount += length;
    }

    private static CompletenessCounts CountCompleteness(IReadOnlyList<McpLogCall> calls)
    {
        var complete = calls.Count(call => call.Completeness == McpLogCompletenessState.Complete);
        var truncated = calls.Count(call => call.Completeness == McpLogCompletenessState.Truncated);
        return new CompletenessCounts(complete, truncated, calls.Count - complete - truncated);
    }

    private static SortedDictionary<string, int> CountBy<T>(IEnumerable<T> source, Func<T, string> selector)
    {
        var result = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in source)
        {
            var key = selector(item);
            result[key] = result.TryGetValue(key, out var count) ? count + 1 : 1;
        }

        return result;
    }

    private sealed record CompletenessCounts(int Complete, int Truncated, int Unknown);
}

internal sealed record McpLogAnalysisResult(McpLogAnalysisReport? Report, string? Error);
