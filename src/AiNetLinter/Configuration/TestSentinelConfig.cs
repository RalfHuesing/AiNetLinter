#nullable enable
namespace AiNetLinter.Configuration;

/// <summary>
/// Konfiguration für den Static Test Sentinel (flexible Testabdeckungserkennung).
/// </summary>
public sealed record TestSentinelConfig
{
    public IReadOnlyList<string> ClassNamePatterns { get; init; } =
    [
        "{Name}Tests",
        "{Name}Test",
        "{Name}IntegrationTests",
        "{Name}*Tests",
    ];

    public bool RecognizeTypeofReference { get; init; } = true;
    public bool RecognizeCoversComment { get; init; } = true;

    /// <summary>
    /// Klassen deren Name mit einem dieser Suffixe endet, werden vom StaticTestSentinel ausgenommen.
    /// Beispiel: ["Extensions", "Constants", "Converter"]
    /// </summary>
    public IReadOnlyCollection<string> ExemptClassNameSuffixes { get; init; }
        = ["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"];

    /// <summary>
    /// Klassen die von einem dieser Typen erben oder diese Interfaces implementieren,
    /// werden vom StaticTestSentinel ausgenommen.
    /// Beispiel: ["ComponentBase", "IValueConverter", "Profile"]
    /// </summary>
    public IReadOnlyCollection<string> ExemptWhenInheritsFrom { get; init; }
        = ["ComponentBase", "IValueConverter", "Profile"];

    /// <summary>
    /// Statische Klassen werden vom StaticTestSentinel ausgenommen wenn true.
    /// </summary>
    public bool ExemptStaticClasses { get; init; } = true;

    /// <summary>
    /// Projekt-Name-Suffixe, die ein Projekt als Testprojekt kennzeichnen,
    /// wenn keine bekannten Testrahmenbibliotheken in den Metadaten gefunden wurden.
    /// Standard: ["Tests", "Test", "IntegrationTests", "Specs", "Spec"].
    /// </summary>
    public IReadOnlyList<string> TestProjectNameSuffixes { get; init; }
        = ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];

    /// <summary>
    /// Wendet Projekt-Overrides an und gibt eine neue Instanz mit den überschriebenen Werten zurück.
    /// </summary>
    public TestSentinelConfig Apply(TestSentinelConfigOverride? @override)
    {
        if (@override == null) return this;
        return this with
        {
            ExemptClassNameSuffixes = @override.ExemptClassNameSuffixes ?? ExemptClassNameSuffixes,
            ExemptWhenInheritsFrom = @override.ExemptWhenInheritsFrom ?? ExemptWhenInheritsFrom,
            ExemptStaticClasses = @override.ExemptStaticClasses ?? ExemptStaticClasses,
            TestProjectNameSuffixes = @override.TestProjectNameSuffixes ?? TestProjectNameSuffixes,
        };
    }
}
