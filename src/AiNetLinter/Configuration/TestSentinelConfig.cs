#nullable enable
namespace AiNetLinter.Configuration;

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

    public IReadOnlyCollection<string> ExemptClassNameSuffixes { get; init; }
        = ["Extensions", "Constants", "Converter", "Profile", "Seed", "Migration", "Startup", "Module"];

    public IReadOnlyCollection<string> ExemptWhenInheritsFrom { get; init; }
        = ["ComponentBase", "IValueConverter", "Profile"];

    public bool ExemptStaticClasses { get; init; } = true;

    public IReadOnlyList<string> TestProjectNameSuffixes { get; init; }
        = ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];

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
