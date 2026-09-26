#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Registration;

internal sealed record LongRunningAssemblyCallRequest(
    ProjectRegistry Registry,
    RequestContext<CallToolRequestParams> Context,
    string TargetPath,
    string ToolName,
    string? OperationToken,
    Func<CancellationToken, Task<CallToolResult>> Call);

internal static class LongRunningAssemblyToolCall
{
    internal static Task<CallToolResult> ExecuteAsync(
        LongRunningAssemblyCallRequest request,
        CancellationToken requestCancellationToken)
    {
        var argumentsKey = CreateArgumentsKey(request.Context.Params.Arguments);
        return LongRunningProjectToolCall.ExecuteRoutedAsync(request.Registry,
            new LongRunningRegistryCallRequest(request.TargetPath, request.ToolName, argumentsKey,
                request.OperationToken, request.Call),
            requestCancellationToken);
    }

    internal static string CreateArgumentsKey(IDictionary<string, JsonElement>? arguments) =>
        JsonSerializer.Serialize(arguments?
            .Where(item => !string.Equals(item.Key, "operationToken", StringComparison.Ordinal))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray());
}
