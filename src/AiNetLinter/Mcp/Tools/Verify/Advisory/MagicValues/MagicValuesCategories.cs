#nullable enable

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Fachliche Kategorien fuer <c>find_magic_values</c>-Funde. Stabile, in JSON-RPC-Aufrufen
/// verwendete String-Repraesentation ueber <see cref="ToStringValue"/> — die Strings landen
/// im Content und im Tool-Argument <c>categoryFilter</c>.
/// </summary>
internal enum MagicValueCategory
{
    /// <summary>Konfigurations-Kandidat: URL, Pfad, Connection-String, Timeout. Empfehlung
    /// <c>appsettings.json</c> oder zentrale Option-Klasse.</summary>
    ConfigCandidates,

    /// <summary>Konstanten-Kandidat: Format-String, Schwellenwert, wiederkehrender Wert.
    /// Empfehlung zentrale <c>Constants.cs</c>.</summary>
    ConstantCandidates,

    /// <summary>Enum-Kandidat: diskreter Wertebereich in switch/if-Kaskaden.</summary>
    EnumCandidates,

    /// <summary><c>nameof(...)</c>-Kandidat: String entspricht einem Symbol-Namen im Scope.</summary>
    NameofCandidates,

    /// <summary>Lokalisierungs-Kandidat: User-Facing Text in Exception/UI-Prompts.</summary>
    LocalizationCandidates,

    /// <summary>Standard-Kandidat: HTTP-Statuscode, Framework-Konstante. Empfehlung
    /// <c>StatusCodes.StatusXXX...</c>.</summary>
    StandardCandidates,

    /// <summary>Security-Kandidat: hartcodiertes Secret/Credential.</summary>
    SecurityCandidates,
}

/// <summary>
/// String-Repraesentation der <see cref="MagicValueCategory"/> fuer JSON-RPC und
/// Content. Stabile snake_case-Werte für Tool-Argumente.
/// </summary>
internal static class MagicValueCategoryExtensions
{
    internal static string ToStringValue(this MagicValueCategory category) => category switch
    {
        MagicValueCategory.ConfigCandidates => "config_candidates",
        MagicValueCategory.ConstantCandidates => "constant_candidates",
        MagicValueCategory.EnumCandidates => "enum_candidates",
        MagicValueCategory.NameofCandidates => "nameof_candidates",
        MagicValueCategory.LocalizationCandidates => "localization_candidates",
        MagicValueCategory.StandardCandidates => "standard_candidates",
        MagicValueCategory.SecurityCandidates => "security_candidates",
        _ => category.ToString().ToLowerInvariant(),
    };

    /// <summary>Liefert die saemtlichen gueltigen Kategorie-IDs in stabiler Reihenfolge — fuer
    /// die <c>INVALID_ARGUMENT</c>-Hint-Liste bei unbekanntem <c>categoryFilter</c>-Argument.</summary>
    internal static string AllCategoryIds() =>
        string.Join(", ",
        [
            "all",
            "config_candidates",
            "constant_candidates",
            "enum_candidates",
            "nameof_candidates",
            "localization_candidates",
            "standard_candidates",
            "security_candidates",
        ]);

    internal static int GetPriority(this MagicValueCategory category) => category switch
    {
        MagicValueCategory.SecurityCandidates => 0,
        MagicValueCategory.ConfigCandidates => 1,
        MagicValueCategory.EnumCandidates => 2,
        MagicValueCategory.StandardCandidates => 3,
        MagicValueCategory.ConstantCandidates => 4,
        MagicValueCategory.NameofCandidates => 5,
        MagicValueCategory.LocalizationCandidates => 6,
        _ => int.MaxValue,
    };

    internal static (string EvidenceBoundary, string Recommendation) Semantics(this MagicValueCategory category) => category switch
    {
        MagicValueCategory.ConfigCandidates => (
            "Statisches URL-/Pfad-/Connection-String-/Timeout-Muster im angeforderten C#-Scope; kein Nachweis einer konkreten Laufzeitkonfiguration.",
            "Konfigurationswert in appsettings.json oder eine typisierte Options-Klasse auslagern."),
        MagicValueCategory.ConstantCandidates => (
            "Statisches Format-String-/Schwellenwert-/Wiederholungs- oder const-Duplikat-Muster im angeforderten C#-Scope; keine Aussage über fachliche Änderungsfrequenz.",
            "Wert als benannte Konstante in einer fachlich passenden Constants-Klasse bündeln."),
        MagicValueCategory.EnumCandidates => (
            "Statische Folge von mindestens drei Vergleichen desselben Identifiers im angeforderten C#-Scope; keine Aussage über weitere dynamische Werte.",
            "Diskreten Wertebereich als Enum modellieren und die Vergleichskaskade darauf umstellen."),
        MagicValueCategory.NameofCandidates => (
            "Statischer exakter Abgleich eines String-Literals mit einem Symbolnamen im angeforderten C#-Scope; keine Laufzeit- oder Serialisierungssemantik.",
            "String durch nameof(Symbol) ersetzen, sofern der externe Vertrag dadurch unverändert bleibt."),
        MagicValueCategory.LocalizationCandidates => (
            "Statischer langer Text als Argument eines Exception-Konstruktors im angeforderten C#-Scope; kein Nachweis der tatsächlichen Benutzeroberfläche.",
            "Benutzerorientierten Text über IStringLocalizer oder eine .resx-Ressource beziehen."),
        MagicValueCategory.StandardCandidates => (
            "Statischer HTTP-Statuscode oder kontextgebundene Buffer-Größe im angeforderten C#-Scope; kein Nachweis der konkreten Framework-Version.",
            "Verfügbare Framework-/Standardkonstante wie StatusCodes.StatusXXX oder eine benannte Buffer-Konstante verwenden."),
        MagicValueCategory.SecurityCandidates => (
            "Statisches Secret-/Credential-Muster im angeforderten C#-Scope; kein Nachweis, ob ein Secret-Store zur Laufzeit verwendet wird.",
            "Credential aus dem Quelltext entfernen und über Secret-Store/KeyVault oder sichere Laufzeitkonfiguration beziehen."),
        _ => ("Statische Magic-Value-Heuristik im angeforderten C#-Scope.", "Kandidaten manuell prüfen."),
    };
}
