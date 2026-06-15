#nullable enable

using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using AiNetLinter.Models;

namespace AiNetLinter.Core;

/// <summary>
/// Prüft WPF Code-Behind-Klassen auf minimalistische Implementierung (MVVM-Pattern).
/// </summary>
public sealed partial class LinterAnalyzer : CSharpSyntaxWalker
{
    internal void CheckWpfCodeBehind(ClassDeclarationSyntax node)
    {
        if (_isTestFile) return;
        if (!_config.UiSeparation.WpfRequireMinimalCodeBehind) return;
        if (!IsWpfCodeBehindClass(node)) return;

        var className = node.Identifier.Text;
        if (_config.UiSeparation.WpfExcludeClassNames.Contains(className, System.StringComparer.OrdinalIgnoreCase))
            return;

        var extraMembers = node.Members
            .Where(static m => m is not ConstructorDeclarationSyntax)
            .ToList();

        if (extraMembers.Count == 0) return;

        var extraNames = extraMembers
            .Select(GetMemberDisplayName)
            .OfType<string>()
            .Take(3)
            .ToList();

        _violations.Add(new RuleViolation
        {
            FilePath = _filePath,
            LineNumber = GetLineNumber(extraMembers[0]),
            RuleName = "WpfRequireMinimalCodeBehind",
            Details = $"Die WPF Code-Behind-Klasse '{className}' enthaelt {extraMembers.Count} zusaetzliche Member " +
                      $"({string.Join(", ", extraNames)}). " +
                      "Code-Behind soll nur den Konstruktor mit InitializeComponent() enthalten.",
            Guidance = "Verschiebe alle Event-Handler und Logik in ein ViewModel (MVVM-Pattern). " +
                       "Verwende Commands (ICommand/RelayCommand) statt Event-Handler. " +
                       "Setze den DataContext im XAML (StaticResource) oder im Konstruktor: 'DataContext = new MyViewModel();'. " +
                       "Im Code-Behind soll nur verbleiben: 'public MyWindow() { InitializeComponent(); }'. " +
                       "Suppression moeglich mit: // ainetlinter-disable WpfRequireMinimalCodeBehind"
        });
    }

    private bool IsWpfCodeBehindClass(ClassDeclarationSyntax node)
    {
        if (!node.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)))
            return false;

        if (node.BaseList == null || node.BaseList.Types.Count == 0)
            return false;

        var baseTypeNames = ExtractBaseTypeSimpleNames(node.BaseList);
        var wpfBaseTypes = _config.UiSeparation.WpfCodeBehindBaseTypes;

        return baseTypeNames.Any(name => wpfBaseTypes.Contains(name, System.StringComparer.OrdinalIgnoreCase));
    }

    private static System.Collections.Generic.IEnumerable<string> ExtractBaseTypeSimpleNames(BaseListSyntax baseList)
    {
        foreach (var baseType in baseList.Types)
        {
            var typeName = baseType.Type switch
            {
                IdentifierNameSyntax id => id.Identifier.Text,
                QualifiedNameSyntax qn => qn.Right.Identifier.Text,
                GenericNameSyntax gn => gn.Identifier.Text,
                _ => null
            };
            if (typeName != null) yield return typeName;
        }
    }

    private static string? GetMemberDisplayName(MemberDeclarationSyntax member) => member switch
    {
        MethodDeclarationSyntax m => m.Identifier.Text + "()",
        PropertyDeclarationSyntax p => p.Identifier.Text,
        FieldDeclarationSyntax f => f.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        EventDeclarationSyntax e => e.Identifier.Text,
        EventFieldDeclarationSyntax ef => ef.Declaration.Variables.FirstOrDefault()?.Identifier.Text,
        _ => member.GetType().Name
    };
}
