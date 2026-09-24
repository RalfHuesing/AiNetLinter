#nullable enable

namespace AiNetLinter.Configuration;

/// <summary>Konfiguration der projektbezogenen Dead-Code-Analyse.</summary>
public sealed record DeadCodeConfig
{
    /// <summary>API-Oberflaeche fuer Projekte ohne passendes Override.</summary>
    public string? DefaultApiSurface { get; init; } = "closed_solution";
}

/// <summary>Projektbezogene Ueberschreibung der Dead-Code-Policy.</summary>
public sealed record DeadCodeConfigOverride
{
    /// <summary>Ueberschreibt den globalen API-Oberflaechenwert des Projekts.</summary>
    public string? ApiSurface { get; init; }
}
