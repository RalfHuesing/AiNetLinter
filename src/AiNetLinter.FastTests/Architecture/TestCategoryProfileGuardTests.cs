#nullable enable

using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Architecture;

/// <summary>
/// Kategorien-/Profilguard fuer AiNetLinter.FastTests: Jede Testklasse dieser Assembly besitzt
/// genau einen gueltigen Kategorie-Trait aus {Unit, Component}.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TestCategoryProfileGuardTests
{
    [Fact]
    public void EveryTestClass_HasExactlyOneValidCategoryTrait() =>
        TestCategoryTraitInspector.EnsureEveryTestClassHasExactlyOneValidCategoryTrait(
            typeof(TestCategoryProfileGuardTests).Assembly, "Unit", "Component");
}
