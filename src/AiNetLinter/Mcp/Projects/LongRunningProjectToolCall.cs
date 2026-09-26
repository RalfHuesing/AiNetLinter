#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.Mcp.Projects;

internal sealed record LongRunningProjectCallRequest(
    string TargetPath,
    string ToolName,
    string ArgumentsKey,
    string? OperationToken,
    Func<ProjectLease, CancellationToken, Task<CallToolResult>> Call);

internal sealed record LongRunningRegistryCallRequest(
    string TargetPath,
    string ToolName,
    string ArgumentsKey,
    string? OperationToken,
    Func<CancellationToken, Task<CallToolResult>> Call);

internal static class LongRunningProjectToolCall
{
    private static readonly ConditionalWeakTable<ProjectRegistry, LongRunningToolCallStore> Stores = new();
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromSeconds(15);

    internal static Task<CallToolResult> ExecuteAsync(
        ProjectRegistry registry,
        LongRunningProjectCallRequest request,
        CancellationToken requestCancellationToken) =>
        ExecuteRoutedAsync(registry,
            new LongRunningRegistryCallRequest(
                request.TargetPath, request.ToolName, request.ArgumentsKey, request.OperationToken,
                lifetimeToken => ProjectToolCall.ExecuteAsync(registry, request.TargetPath,
                    lease => request.Call(lease, lifetimeToken))),
            requestCancellationToken);

    internal static Task<CallToolResult> ExecuteRoutedAsync(
        ProjectRegistry registry,
        LongRunningRegistryCallRequest request,
        CancellationToken requestCancellationToken) =>
        Stores.GetValue(registry, key => new LongRunningToolCallStore(ResponseWindow, key.ShutdownToken))
            .RunAsync(new LongRunningToolCallRequest(
                request.ToolName, request.TargetPath, request.ArgumentsKey, request.OperationToken,
                request.Call),
                requestCancellationToken);
}
