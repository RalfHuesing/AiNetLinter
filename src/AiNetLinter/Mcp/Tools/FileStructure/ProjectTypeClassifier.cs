#nullable enable

using System;
using System.IO;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Klassifiziert Roslyn-Projekte heuristisch nach ihrer Rolle (<c>Exe</c>, <c>Test</c>, <c>Lib</c>).
/// </summary>
internal static class ProjectTypeClassifier
{
    private static readonly string[] TestNameTokens = [".Tests", ".FastTests", ".IntegrationTests", ".TestKit", ".Specs"];

    internal static string Classify(Project project)
    {
        if (IsTestProject(project)) return "Test";
        if (IsExecutableProject(project)) return "Exe";
        return "Lib";
    }

    private static bool IsTestProject(Project project)
    {
        var name = project.Name;
        foreach (var token in TestNameTokens)
        {
            if (name.Contains(token, StringComparison.OrdinalIgnoreCase)) return true;
        }

        if (name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) || name.EndsWith("Test", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (project.FilePath is { } path)
        {
            var fileName = Path.GetFileName(path);
            if (PathNormalizer.IsTestFile(fileName) || PathNormalizer.IsTestFile(path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExecutableProject(Project project)
    {
        if (project.CompilationOptions is not { } options) return false;
        return options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication;
    }
}
