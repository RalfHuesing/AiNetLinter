namespace AiNetLinter.Configuration;

/// <summary>
/// Fein-granulare Konfiguration der Magic-Value-Erkennung.
/// </summary>
public sealed record MagicValuesConfig
{
    /// <summary>
    /// Steuert welche Literale als magic gelten.
    /// "all"              — alle String+Numeric (bisheriges Verhalten)
    /// "numeric-only"     — nur Numeric-Literale (außer 0/1/-1)
    /// "numeric-and-short-string" — Numeric + Strings bis MinStringLength Zeichen
    /// </summary>
    public string Mode { get; init; } = "all";

    /// <summary>
    /// Mindestlänge eines Strings damit er als magic gilt (bei Mode numeric-and-short-string).
    /// Default 0 = alle Strings (heutiges Verhalten).
    /// </summary>
    public int MinStringLength { get; init; } = 0;

    /// <summary>
    /// Regex-Muster für String-Literale, die grundsätzlich ignoriert werden.
    /// Beispiel: ["^/[\\w/{}\\-]*$"] ignoriert Routen wie "/api/{id}"
    /// </summary>
    public IReadOnlyCollection<string> IgnoreStringPatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Erweiterter Satz ignorierter Numeric-Werte (zusätzlich zu 0/1/-1).
    /// Beispiel: [2, 100, 1000] für bekannte Timeout/Batch-Größen.
    /// </summary>
    public IReadOnlyCollection<double> IgnoreNumericValues { get; init; } = Array.Empty<double>();

    /// <summary>
    /// String-Literale als direkte Argumente von Methoden deren Name mit einem der
    /// Einträge in IgnoreInvocationPrefixes beginnt, werden ignoriert.
    /// </summary>
    public IReadOnlyCollection<string> IgnoreInvocationPrefixes { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Wenn true: Literale innerhalb von Collection/Dictionary-Initialisierern werden ignoriert.
    /// Für Metadata-over-Code-Muster (JSON-Keys, OAuth-Felder).
    /// </summary>
    public bool IgnoreCollectionInitializers { get; init; } = false;

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// Nur gesetzte (nicht-null) Override-Felder werden angewendet.
    /// </summary>
    public MagicValuesConfig Apply(MagicValuesConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            Mode = @override.Mode ?? Mode,
            MinStringLength = @override.MinStringLength ?? MinStringLength,
            IgnoreStringPatterns = @override.IgnoreStringPatterns ?? IgnoreStringPatterns,
            IgnoreNumericValues = @override.IgnoreNumericValues ?? IgnoreNumericValues,
            IgnoreInvocationPrefixes = @override.IgnoreInvocationPrefixes ?? IgnoreInvocationPrefixes,
            IgnoreCollectionInitializers = @override.IgnoreCollectionInitializers ?? IgnoreCollectionInitializers,
        };
    }
}

/// <summary>
/// Datei- und Verzeichnis-Ausschlüsse für die Linter-Analyse.
/// </summary>
public sealed record FileFiltersConfig
{
    /// <summary>
    /// Glob-Muster die gegen den Dateinamen (ohne Pfad) geprüft werden.
    /// Standard-Wildcards: * und ?
    /// </summary>
    public IReadOnlyCollection<string> ExcludeFilePatterns { get; init; }
        = Array.Empty<string>();

    /// <summary>
    /// Pfad-Segmente: Dateien die eines dieser Segmente im Pfad enthalten, werden übersprungen.
    /// </summary>
    public IReadOnlyCollection<string> ExcludeDirectoryPatterns { get; init; }
        = ["obj/", "bin/"];

    /// <summary>
    /// Wenn true, werden Klassen/Records/Structs mit dem GeneratedCodeAttribute-Attribut übersprungen.
    /// </summary>
    public bool SkipGeneratedCodeAttribute { get; init; } = false;
}
