#nullable enable

using System;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfsklasse zur Erkennung von Testprojekten anhand ihrer Metadatenreferenzen.
/// </summary>
public static class TestProjectDetector
{
    private static readonly string[] TestKeywords = ["xunit", "nunit", "testplatform", "unittesting"];

    /// <summary>
    /// Prüft, ob ein Projekt ein Testprojekt ist.
    /// </summary>
    /// <param name="project">Das zu prüfende Roslyn-Projekt.</param>
    /// <returns>True, wenn das Projekt ein Testprojekt ist; andernfalls False.</returns>
    public static bool IsTestProject(Project project)
    {
        foreach (var reference in project.MetadataReferences)
        {
            if (IsTestReference(reference.Display))
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
