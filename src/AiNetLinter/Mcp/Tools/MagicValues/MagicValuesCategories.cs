#nullable enable

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Fachliche Kategorien fuer <c>find_magic_values</c>-Funde. Stabile, in JSON-RPC-Aufrufen
/// verwendete String-Repraesentation ueber <see cref="ToStringValue"/> — die Strings landen
/// 1:1 im <c>StructuredContent</c> und im Tool-Argument <c>categoryFilter</c>.
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
/// <c>StructuredContent</c>. Stabile snake_case-Werte — Aenderungen wuerden einen
/// API-Bruch fuer MCP-Clients bedeuten.
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
}
