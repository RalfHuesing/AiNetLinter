#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AiNetLinter.TestKit;

internal static class TestCategoryTraitInspector
{
    public static void EnsureEveryTestClassHasExactlyOneValidCategoryTrait(Assembly assembly, params string[] allowedCategories)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(allowedCategories);

        var violations = GetTestClasses(assembly)
            .Select(type => (Type: type, Categories: GetCategoryTraits(type)))
            .Where(entry => entry.Categories.Count != 1 || !allowedCategories.Contains(entry.Categories.Single()))
            .Select(entry => $"{entry.Type.FullName} [{string.Join(",", entry.Categories)}]")
            .ToList();

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                $"Testklassen mit ungueltigem/fehlendem Kategorie-Trait (erlaubt: {string.Join(",", allowedCategories)}): {string.Join("; ", violations)}");
        }
    }

    private static List<Type> GetTestClasses(Assembly assembly) => assembly.GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic)
        .Where(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method => method.GetCustomAttributes().Any(attribute => attribute.GetType().Name is "FactAttribute" or "TheoryAttribute")))
        .ToList();

    private static List<string> GetCategoryTraits(Type type) => type.GetCustomAttributes()
        .Where(attribute => attribute.GetType().Name == "TraitAttribute")
        .Select(attribute => (
            Name: attribute.GetType().GetProperty("Name")?.GetValue(attribute) as string,
            Value: attribute.GetType().GetProperty("Value")?.GetValue(attribute) as string))
        .Where(trait => trait.Name == "Category" && trait.Value is not null)
        .Select(trait => trait.Value!)
        .ToList();
}
