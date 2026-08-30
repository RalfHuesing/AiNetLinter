#nullable enable

using System;
using System.Globalization;

namespace AiNetLinter.Mcp;

internal sealed record AnalysisSymbolIdentity(string ContentHash, long Generation)
{
    internal const string Prefix = "assembly:";

    internal string? Format(string? symbolId) =>
        symbolId is null
            ? null
            : $"{Prefix}{ContentHash}:{Generation.ToString(CultureInfo.InvariantCulture)}:{symbolId}";

    internal bool Matches(AnalysisSymbolIdentity other) =>
        string.Equals(ContentHash, other.ContentHash, StringComparison.OrdinalIgnoreCase)
        && Generation == other.Generation;

    internal static bool TryParse(
        string value,
        out AnalysisSymbolIdentity? identity,
        out string symbolId)
    {
        identity = null;
        symbolId = string.Empty;
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var hashStart = Prefix.Length;
        var hashEnd = value.IndexOf(':', hashStart);
        if (hashEnd <= hashStart) return false;

        var generationStart = hashEnd + 1;
        var generationEnd = value.IndexOf(':', generationStart);
        if (generationEnd <= generationStart || generationEnd == value.Length - 1) return false;
        if (!long.TryParse(
                value[generationStart..generationEnd],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var generation))
        {
            return false;
        }

        var hash = value[hashStart..hashEnd];
        symbolId = value[(generationEnd + 1)..];
        identity = new AnalysisSymbolIdentity(hash, generation);
        return true;
    }
}
