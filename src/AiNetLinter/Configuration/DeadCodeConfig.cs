#nullable enable

namespace AiNetLinter.Configuration;

/// <summary>Konfiguration der projektbezogenen Dead-Code-Analyse.</summary>
public sealed record DeadCodeConfig
{
    /// <summary>Externe API-Oberflaeche; fehlende Angaben bleiben bewusst unentschieden.</summary>
    public string? DefaultApiSurface { get; init; } = "unknown";
}

/// <summary>Projektbezogene Ueberschreibung der Dead-Code-Policy.</summary>
public sealed record DeadCodeConfigOverride
{
    /// <summary>Ueberschreibt den globalen API-Oberflaechenwert des Projekts.</summary>
    public string? ApiSurface { get; init; }
}
