#nullable enable

using System;
using System.Linq;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Architecture;

/// <summary>
/// Kategorien-/Profilguard fuer AiNetLinter.IntegrationTests (konzept.md Leitplanke 6,
/// "Kategorisierung als Architekturvertrag"): jede Testklasse dieser Assembly besitzt genau einen
/// gueltigen Kategorie-Trait aus {Integration, Dogfood, Performance, Stress}. Getrennt von der
/// gleichnamigen Klasse in AiNetLinter.FastTests, um keine fragile Sibling-Assembly-Pfadlogik
/// zwischen zwei Testprojekten mit potenziell unterschiedlichem Build-Zustand zu brauchen.
/// </summary>
[Trait("Category", "Integration")]
public sealed class TestCategoryProfileGuardTests
{
    private static readonly string[] AllowedCategories = { "Integration", "Dogfood", "Performance", "Stress" };

    [Fact]
    public void EveryTestClass_HasExactlyOneValidCategoryTrait()
    {
        var violations = TestCategoryTraitInspector.GetTestClasses(typeof(TestCategoryProfileGuardTests).Assembly)
            .Select(type => (Type: type, Categories: TestCategoryTraitInspector.GetCategoryTraits(type)))
            .Where(entry => entry.Categories.Count != 1 || !AllowedCategories.Contains(entry.Categories.Single()))
            .Select(entry => $"{entry.Type.FullName} [{string.Join(",", entry.Categories)}]")
            .ToList();

        Assert.True(violations.Count == 0,
            $"Testklassen mit ungueltigem/fehlendem Kategorie-Trait (erlaubt: {string.Join(",", AllowedCategories)}): {string.Join("; ", violations)}");
    }
}
