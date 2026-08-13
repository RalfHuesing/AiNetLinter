#nullable enable

using System;
using System.Linq;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Architecture;

/// <summary>
/// Kategorien-/Profilguard fuer AiNetLinter.FastTests (konzept.md Leitplanke 6,
/// "Kategorisierung als Architekturvertrag"): jede Testklasse dieser Assembly besitzt genau einen
/// gueltigen Kategorie-Trait aus {Unit, Component}. Hilfs-/Fixtureklassen ohne eigene
/// [Fact]/[Theory]-Methode sind ausgenommen. Reflektiert ueber die bereits geladene eigene
/// Assembly statt Metadaten-Reader, weil hier -- anders als beim Deny-Listen-Guard -- keine
/// zusaetzliche Assembly von der Platte gelesen werden muss.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TestCategoryProfileGuardTests
{
    private static readonly string[] AllowedCategories = { "Unit", "Component" };

    [Fact]
    public void EveryTestClass_HasExactlyOneValidCategoryTrait()
    {
        var violations = TestCategoryTraitInspector
            .GetTestClasses(typeof(TestCategoryProfileGuardTests).Assembly)
            .Select(type => (Type: type, Categories: TestCategoryTraitInspector.GetCategoryTraits(type)))
            .Where(entry => entry.Categories.Count != 1 || !AllowedCategories.Contains(entry.Categories.Single()))
            .Select(entry => $"{entry.Type.FullName} [{string.Join(",", entry.Categories)}]")
            .ToList();

        Assert.True(violations.Count == 0,
            $"Testklassen mit ungueltigem/fehlendem Kategorie-Trait (erlaubt: {string.Join(",", AllowedCategories)}): {string.Join("; ", violations)}");
    }
}
