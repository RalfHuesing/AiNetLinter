#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal static class RazorGeneratedDeclarations
{
    internal static bool HasMatchingGeneratedDeclaration(INamedTypeSymbol type, string componentPath, Project project)
    {
        var projectDirectory = GetProjectDirectory(project);
        var expectedPath = NormalizeRelativePath(projectDirectory, componentPath);
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax();
            if (declaration is not TypeDeclarationSyntax typeDeclaration
                || !typeDeclaration.Modifiers.Any(SyntaxKind.PartialKeyword)) continue;
            var generatedPath = GetComponentPath(declaration.SyntaxTree.GetRoot());
            if (generatedPath is not null
                && NormalizeRelativePath(projectDirectory, generatedPath).Equals(expectedPath, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string? GetComponentPath(SyntaxNode syntaxRoot)
    {
        foreach (var trivia in syntaxRoot.DescendantTrivia(descendIntoTrivia: true))
        {
            if (trivia.GetStructure() is not PragmaChecksumDirectiveTriviaSyntax checksum) continue;
            var filePath = checksum.File.ValueText;
            if (filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase)) return filePath;
        }

        return null;
    }

    private static string NormalizeRelativePath(string projectDirectory, string path)
    {
        var absolutePath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectDirectory, path));
        return Path.GetRelativePath(projectDirectory, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string GetProjectDirectory(Project project) =>
        Path.GetDirectoryName(project.FilePath)
        ?? Path.GetDirectoryName(project.Solution.FilePath)
        ?? Environment.CurrentDirectory;
}
