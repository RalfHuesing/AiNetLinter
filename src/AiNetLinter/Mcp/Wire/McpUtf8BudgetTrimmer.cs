#nullable enable

using System;
using System.Text;

namespace AiNetLinter.Mcp.Wire;

internal static class McpUtf8BudgetTrimmer
{
    internal static string TrimWithoutEllipsis(string value, int maxBytes)
    {
        if (maxBytes <= 0) return string.Empty;
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;

        var low = 0;
        var high = value.Length;
        while (low < high)
        {
            var middle = low + ((high - low + 1) / 2);
            if (Encoding.UTF8.GetByteCount(value.AsSpan(0, middle)) <= maxBytes)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (low > 0 && low < value.Length && char.IsHighSurrogate(value[low - 1])) low--;
        return value[..low];
    }
}
