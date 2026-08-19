#nullable enable

using AiNetLinter.Core;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.FileStructure;

/// <summary>
/// Klassifiziert Roslyn-Projekte heuristisch nach ihrer Rolle (<c>Exe</c>, <c>Test</c>, <c>Lib</c>).
/// </summary>
internal static class ProjectTypeClassifier
{
    internal static string Classify(Project project)
    {
        if (TestDetector.IsTestProject(project)) return "Test";
        if (IsExecutableProject(project)) return "Exe";
        return "Lib";
    }

    private static bool IsExecutableProject(Project project)
    {
        if (project.CompilationOptions is not { } options) return false;
        return options.OutputKind is OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication;
    }
}
