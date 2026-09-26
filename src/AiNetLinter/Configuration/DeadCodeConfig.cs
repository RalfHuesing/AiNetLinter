#nullable enable

namespace AiNetLinter.Configuration;

/// <summary>Konfiguration der projektbezogenen Dead-Code-Analyse.</summary>
public sealed record DeadCodeConfig
{
    /// <summary>API-Oberflaeche fuer Projekte ohne passendes Override.</summary>
    public string? DefaultApiSurface { get; init; } = "closed_solution";
    public int VerifyBudgetSeconds { get; init; } = 10;
    public int SolutionBudgetSeconds { get; init; } = 60;
    public int MaxCandidateGroups { get; init; } = 20;
    public int MaxResponseBytes { get; init; } = 8192;
    public System.Collections.Generic.Dictionary<string, string> ProjectRoles { get; init; } = new(System.StringComparer.Ordinal);
}

/// <summary>Projektbezogene Ueberschreibung der Dead-Code-Policy.</summary>
public sealed record DeadCodeConfigOverride
{
    /// <summary>Ueberschreibt den globalen API-Oberflaechenwert des Projekts.</summary>
    public string? ApiSurface { get; init; }
}
