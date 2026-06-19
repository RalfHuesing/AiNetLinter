#nullable enable

using System.Text;

namespace AiNetLinter.Output;

/// <summary>
/// Erzeugt strukturierte Fehlermeldungen fuer maschinenlesbares Parsing durch Agenten.
/// Format: [ERROR]: {code}: {message}[\n  context: {context}][\n  hint: {hint}]
/// </summary>
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
