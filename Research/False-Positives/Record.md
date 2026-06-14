# Fehler
MaxConstructorDependencies | Der Primaerkonstruktor hat 13 Parameter (erlaubt sind maximal 5, Framework-Typen nicht gezaehlt). -> Reduziere die Anzahl der Abhaengigkeiten, indem du den Typ in kleinere Klassen aufteilst.

# Beispiel Code

public sealed record AiNetLinterRunOptions(
    bool WaveReady = false,
    bool DebtReport = false,
    bool OnlyChanged = false,
    bool SyncCursorRules = false,
    bool CheckOnly = false,
    bool ReadmeOnly = false,
    string? GitSince = null,
    string? BaselinePath = null,
    string? PlaybookPath = null,
    string? GraphPath = null,
    string? ImpactGitRef = null,
    string? FootprintClassName = null,
    string OutputFormat = "text")
{
    public static AiNetLinterRunOptions Default { get; } = new();
}

# Änderungs vorschlag im Code

private void CheckPrimaryConstructorDependencies(TypeDeclarationSyntax node)
{
    if (node.ParameterList == null) return;
    var count = node.ParameterList.Parameters.Count;
    
    if (count > _config.Metrics.MaxConstructorDependencies)
    {
        // Roslyn-Typenprüfung: Handelt es sich um einen Record?
        if (node is RecordDeclarationSyntax)
        {
            _violations.Add(new RuleViolation
            {
                FilePath = _filePath,
                LineNumber = GetLineNumber(node),
                RuleName = "MaxRecordInitializationParameters", // Eigene Regel für LLM-Klarheit
                Details = $"Der Konfigurations-Record '{node.Identifier.Text}' hat {count} optionale Parameter im Primärkonstruktor.",
                Guidance = "Für LLM-Optimierung: Wandle die positional Parameter in Standard-Properties mit '{ get; init; }' um. Große Konstruktoren führen bei KI-Generierung zu Positionsfehlern."
            });
            return; // Verhindert das Durchrutschen in die normale DI-Meldung
        }

        // Standard-Logik für normale Klassen (Services mit Dependency Injection)
        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(node),
            RuleName = "MaxConstructorDependencies",
            Details = $"Der Primaerkonstruktor hat {count} Parameter (erlaubt sind maximal {_config.Metrics.MaxConstructorDependencies}).",
            Guidance = "Reduziere die Anzahl der Abhaengigkeiten, indem du den Typ in kleinere Klassen aufteilst."
        });
    }
}