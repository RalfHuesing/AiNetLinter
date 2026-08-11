namespace AiNetLinter.Models;

/// <summary>
/// Baut einen <see cref="RuleViolation"/> auf Datei-Ebene (<c>LineNumber = 1</c>, kein konkreter
/// Fundort) — zentral statt separat in <see cref="AiNetLinter.Core.Checkers.UiFileSeparationChecker"/>,
/// <see cref="AiNetLinter.Web.CssAnalyzer"/> und <see cref="AiNetLinter.Web.JsAnalyzer"/> dupliziert.
/// Alle bisherigen Aufrufer melden Datei-Ebene-Befunde (kein konkreter Fundort), daher kein
/// <c>lineNumber</c>-Parameter — <see cref="RuleViolation"/> selbst bleibt bei Bedarf per
/// Object-Initializer erreichbar.
/// </summary>
public static class RuleViolationFactory
{
    public static RuleViolation Create(string filePath, string ruleName, string details, string guidance) =>
        new RuleViolation
        {
            FilePath = filePath,
            LineNumber = 1,
            RuleName = ruleName,
            Details = details,
            Guidance = guidance,
        };
}
