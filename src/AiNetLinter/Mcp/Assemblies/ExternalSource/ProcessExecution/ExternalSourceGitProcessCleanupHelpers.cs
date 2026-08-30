#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.ProcessExecution;

internal static class ExternalSourceGitProcessCleanupHelpers
{
    internal static bool IsUsableHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);

    internal static Exception? CombineFailures(
        ICollection<Exception> failures,
        string aggregateMessage)
    {
        if (failures.Count == 0)
        {
            return null;
        }

        if (failures.Count == 1)
        {
            foreach (var failure in failures)
            {
                return failure;
            }
        }

        return new AggregateException(aggregateMessage, failures);
    }
}
