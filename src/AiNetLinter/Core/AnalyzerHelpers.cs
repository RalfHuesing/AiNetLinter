using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfsmethoden für die statische Code-Analyse.
/// </summary>
internal static class AnalyzerHelpers
{
    public static bool IsTestFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        return CheckTestPath(filePath);
    }

    private static bool CheckTestPath(string path)
    {
        if (path.EndsWith("Tests.cs")) return true;
        if (path.EndsWith("Test.cs")) return true;
        return path.Contains($"{Path.DirectorySeparatorChar}Tests{Path.DirectorySeparatorChar}") ||
               path.Contains("/Tests/") ||
               path.Contains("\\Tests\\");
    }

    public static bool IsInPublicContext(SyntaxNode node)
    {
        if (node is MemberDeclarationSyntax member && !IsPublic(member))
        {
            return false;
        }
        return CheckParentPublicContext(node.Parent);
    }

    private static bool IsPublic(MemberDeclarationSyntax member)
    {
        return member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
    }

    private static bool CheckParentPublicContext(SyntaxNode? parent)
    {
        if (parent == null) return true;
        if (parent is TypeDeclarationSyntax typeDecl && !typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
        {
            return false;
        }
        return CheckParentPublicContext(parent.Parent);
    }

    public static bool IsNullableEnabledGlobally(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir != null)
        {
            var csprojFiles = SafeGetCsprojFiles(dir);
            if (csprojFiles.Length > 0)
            {
                return CheckCsprojListForNullable(csprojFiles);
            }
            dir = Path.GetDirectoryName(dir);
        }
        return false;
    }

    private static string[] SafeGetCsprojFiles(string dir)
    {
        try
        {
            return Directory.GetFiles(dir, "*.csproj");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Csproj error: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static bool CheckCsprojListForNullable(string[] csprojFiles)
    {
        foreach (var csproj in csprojFiles)
        {
            if (IsCsprojNullableEnabled(csproj)) return true;
        }
        return false;
    }

    private static bool IsCsprojNullableEnabled(string csproj)
    {
        try
        {
            var content = File.ReadAllText(csproj);
            return content.Contains("<Nullable>enable</Nullable>") || 
                   content.Contains("<Nullable>annotations</Nullable>");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Csproj check error: {ex.Message}");
            return false;
        }
    }
}
