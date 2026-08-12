#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        var assembly = typeof(TestCategoryProfileGuardTests).Assembly;
        var testClasses = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsPublic)
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(m => m.GetCustomAttributes().Any(a => a.GetType().Name is "FactAttribute" or "TheoryAttribute")));

        var violations = new List<string>();
        foreach (var type in testClasses)
        {
            var categories = GetCategoryTraits(type);
            if (categories.Count != 1 || !AllowedCategories.Contains(categories[0]))
            {
                violations.Add($"{type.FullName} [{string.Join(",", categories)}]");
            }
        }

        Assert.True(violations.Count == 0,
            $"Testklassen mit ungueltigem/fehlendem Kategorie-Trait (erlaubt: {string.Join(",", AllowedCategories)}): {string.Join("; ", violations)}");
    }

    private static List<string> GetCategoryTraits(Type type)
    {
        return type.GetCustomAttributes()
            .Where(a => a.GetType().Name == "TraitAttribute")
            .Select(a => (
                Name: a.GetType().GetProperty("Name")?.GetValue(a) as string,
                Value: a.GetType().GetProperty("Value")?.GetValue(a) as string))
            .Where(t => t.Name == "Category" && t.Value != null)
            .Select(t => t.Value!)
            .ToList();
    }
}
