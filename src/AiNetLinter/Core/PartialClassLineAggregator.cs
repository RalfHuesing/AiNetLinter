using AiNetLinter.Configuration;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Aggregiert Zeilenanzahl über partial-Klassenteile hinweg.
/// </summary>
public static class PartialClassLineAggregator
{
    /// <summary>
    /// Erzeugt MaxLineCount-Verstöße für partial-Typen, deren Summe das Limit überschreitet.
    /// </summary>
    public static IReadOnlyCollection<RuleViolation> BuildViolations(
        IReadOnlyCollection<PartialClassPart> parts,
        Config config)
    {
        if (!config.Metrics.AggregatePartialClassLineCount || parts.Count == 0)
        {
            return [];
        }

        return parts
            .GroupBy(p => p.TypeName, StringComparer.Ordinal)
            .Select(group => TryCreateViolation(group, config))
            .Where(v => v != null)
            .Cast<RuleViolation>()
            .ToArray();
    }

    private static RuleViolation? TryCreateViolation(IGrouping<string, PartialClassPart> group, Config config)
    {
        var totalLines = group.Sum(p => p.FileLineCount);
        if (totalLines <= config.Metrics.MaxLineCount)
        {
            return null;
        }

        var first = group.OrderBy(p => p.FilePath, StringComparer.OrdinalIgnoreCase).First();
        return new RuleViolation
        {
            FilePath = first.FilePath,
            LineNumber = first.LineNumber,
            RuleName = nameof(config.Metrics.MaxLineCount),
            Details = $"Der partial-Typ '{group.Key}' hat insgesamt {totalLines} Zeilen ueber {group.Count()} Dateien (erlaubt sind maximal {config.Metrics.MaxLineCount}).",
            Guidance = "Teile den partial-Typ in kleinere, logisch getrennte Typen oder reduziere die Dateigroesse der Teile.",
        };
    }
}

/// <summary>
/// Ein partial-Klassenteil mit Dateizeilenanzahl.
/// </summary>
public sealed record PartialClassPart(
    string TypeName,
    string FilePath,
    int LineNumber,
    int FileLineCount);
