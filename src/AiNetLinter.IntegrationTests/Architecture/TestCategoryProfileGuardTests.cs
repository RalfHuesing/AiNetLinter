#nullable enable

using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Architecture;

/// <summary>
/// Kategorien-/Profilguard fuer AiNetLinter.IntegrationTests: Jede Testklasse dieser Assembly
/// besitzt genau einen gueltigen Kategorie-Trait aus {Integration, Dogfood, Performance, Stress}.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TestCategoryProfileGuardTests
{
    [Fact]
    public void EveryTestClass_HasExactlyOneValidCategoryTrait() =>
        TestCategoryTraitInspector.EnsureEveryTestClassHasExactlyOneValidCategoryTrait(
            typeof(TestCategoryProfileGuardTests).Assembly, "Integration", "Dogfood", "Performance", "Stress");
}
