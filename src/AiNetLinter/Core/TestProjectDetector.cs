#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfsklasse zur Erkennung von Testprojekten anhand ihrer Metadatenreferenzen und Projektnamen.
/// </summary>
public static class TestProjectDetector
{
    private static readonly string[] TestKeywords = ["xunit", "nunit", "testplatform", "unittesting"];

    private static readonly string[] DefaultTestProjectNameSuffixes =
        ["Tests", "Test", "IntegrationTests", "Specs", "Spec"];

    /// <summary>
    /// Prüft, ob ein Projekt ein Testprojekt ist.
    /// Primär via Metadatenreferenzen, Fallback über Projektnamen-Suffixe.
    /// </summary>
    /// <param name="project">Das zu prüfende Roslyn-Projekt.</param>
    /// <param name="testProjectNameSuffixes">
    /// Optionale Projektnamen-Suffixe (Fallback). Null = Standardliste verwenden.
    /// </param>
    /// <returns>True, wenn das Projekt ein Testprojekt ist; andernfalls False.</returns>
    public static bool IsTestProject(Project project, IReadOnlyList<string>? testProjectNameSuffixes = null)
    {
        foreach (var reference in project.MetadataReferences)
        {
            if (IsTestReference(reference.Display))
            {
                return true;
            }
        }

        var suffixes = testProjectNameSuffixes ?? DefaultTestProjectNameSuffixes;
        return HasTestProjectNameSuffix(project.Name, suffixes);
    }

    private static bool HasTestProjectNameSuffix(string projectName, IReadOnlyList<string> suffixes)
    {
        foreach (var suffix in suffixes)
        {
            if (projectName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                || projectName.EndsWith("." + suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsTestReference(string? display)
    {
        if (string.IsNullOrEmpty(display))
        {
            return false;
        }

        foreach (var keyword in TestKeywords)
        {
            if (display.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
