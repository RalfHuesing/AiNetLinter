namespace AiNetLinter.Models;

/// <summary>
/// Repräsentiert einen Regelverstoß im Quellcode.
/// </summary>
public sealed record RuleViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string RuleName { get; init; }
    public required string Details { get; init; }
    public required string Guidance { get; init; }

    /// <summary>
    /// Formatiert den Verstoß in eine strukturierte, AI-lesbare Terminalausgabe.
    /// </summary>
    public override string ToString()
    {
        return $"[ARCH-ERROR]: Die Regel '{RuleName}' in '{FilePath}' auf Zeile {LineNumber} wurde verletzt.\n" +
               $"- Details: {Details}\n" +
               $"- Anleitung: {Guidance}\n";
    }
}
