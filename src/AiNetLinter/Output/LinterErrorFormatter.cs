#nullable enable

using System.Text;

namespace AiNetLinter.Output;

internal static class LinterErrorFormatter
{
    internal static string Format(string code, string message, string? context = null, string? hint = null)
    {
        var sb = new StringBuilder();
        sb.Append($"[ERROR]: {code}: {message}");
        if (!string.IsNullOrWhiteSpace(context))
            sb.Append($"\n  context: {context}");
        if (!string.IsNullOrWhiteSpace(hint))
            sb.Append($"\n  hint:    {hint}");
        return sb.ToString();
    }
}
