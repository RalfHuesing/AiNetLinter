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

    private enum NullableState
    {
        None,
        Enabled,
        Disabled
    }

    public static bool IsNullableEnabledGlobally(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        while (dir != null)
        {
            var state = CheckDirectoryForNullable(dir);
            if (state == NullableState.Enabled) return true;
            if (state == NullableState.Disabled) return false;

            dir = Path.GetDirectoryName(dir);
        }
        return false;
    }

    private static NullableState CheckDirectoryForNullable(string dir)
    {
        var propsState = CheckPropsFile(dir);
        if (propsState != NullableState.None) return propsState;

        return CheckCsprojFiles(dir);
    }

    private static NullableState CheckPropsFile(string dir)
    {
        var propsPath = Path.Combine(dir, "Directory.Build.props");
        if (File.Exists(propsPath))
        {
            return GetNullableStateFromXml(propsPath);
        }
        return NullableState.None;
    }

    private static NullableState CheckCsprojFiles(string dir)
    {
        var csprojFiles = SafeGetCsprojFiles(dir);
        foreach (var csproj in csprojFiles)
        {
            var state = GetNullableStateFromXml(csproj);
            if (state != NullableState.None) return state;
        }
        return NullableState.None;
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

    private static NullableState GetNullableStateFromXml(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            if (content.Contains("<Nullable>enable</Nullable>") || 
                content.Contains("<Nullable>annotations</Nullable>"))
            {
                return NullableState.Enabled;
            }
            if (content.Contains("<Nullable>disable</Nullable>"))
            {
                return NullableState.Disabled;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Xml read error: {ex.Message}");
        }
        return NullableState.None;
    }
}
