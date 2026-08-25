#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// String- und Number-spezifische Sub-Heuristiken fuer <see cref="MagicValuesClassifier"/>:
/// <c>nameof_candidates</c>, <c>security_candidates</c>, <c>localization_candidates</c> und
/// die <c>standard_candidates</c>-Erweiterung um nicht-HTTP Magic Numbers. Aus den
/// Hauptdateien in eine eigene Datei extrahiert, damit <see cref="MagicValuesClassifier"/>
/// und <see cref="MagicValuesNumberClassifier"/> unter dem <c>MaxLineCount: 500</c>-Limit
/// bleiben (siehe <c>AiNetLinter.mdc</c>).
/// </summary>
internal static class MagicValuesStringHeuristics
{
    // Parameternamen, die auf ein Secret/Credential hindeuten (CWE-798).
    private static readonly HashSet<string> SecurityNameKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "secret", "apikey", "token", "connectionstring", "credential", "auth",
    };

    // Bekannte Praefixe fuer hartcodierte Secrets (Cloud-Provider, GitHub-PAT, Slack).
    private static readonly string[] SecurityPrefixes =
    {
        "AKIA", "sk-", "ghp_", "xoxb-",
    };

    // Schluesselwoerter im Literal, die auf einen Connection-String hindeuten
    // (z. B. "Server=", "Database=", ...).
    private static readonly string[] ConnectionStringKeywords =
    [
        "Server=", "Database=", "Trusted_Connection=", "User Id=", "Password=", "Data Source=",
    ];

    // Strukturelle Heuristik-Schemata (URL, Windows-Pfad). Werden in ClassifyString
    // abgefragt; Prefix-Listen zentral, damit Aenderungen an einer Stelle wirken.
    private static readonly string[] UrlPrefixes =
    {
        "http://", "https://", "ftp://",
    };

    // Well-known Buffer-Konstanten, die semantisch eindeutig sind (1024 = 1 KiB Buffer).
    // Zeit-Konstanten wurden ersatzlos gestrichen, da deren Bedeutung vom Kontext am Verwendungsort abhängt.
    private static readonly HashSet<int> StandardBufferNumbers = new()
    {
        1024, 2048, 4096, 8192,
    };

    // Empfehlungs-Mapping fuer die StandardBufferNumbers — wird im Recommendation-String
    // verwendet, damit der Refactor-Hint lesbar bleibt (BufferSize).
    private static readonly Dictionary<int, string> StandardBufferNames = new()
    {
        [1024] = "BufferSize (1 KiB)",
        [2048] = "BufferSize (2 KiB)",
        [4096] = "BufferSize (4 KiB)",
        [8192] = "BufferSize (8 KiB)",
    };

    // Exception-Typen, die als Heuristik fuer User-Facing-Message-Texte gelten.
    private static readonly HashSet<string> ExceptionTypeNames = new(StringComparer.Ordinal)
    {
        "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException",
        "InvalidOperationException", "NotSupportedException", "NotImplementedException",
        "FormatException", "InvalidCastException", "OperationCanceledException",
        "ApplicationException", "Exception",
    };

    /// <summary>Prueft, ob ein String-Literal einen Connection-String-Schluesselwoert (Server=,
    /// Database= usw.) enthaelt. Liefert eine <c>config_candidates</c>-Klassifizierung mit
    /// appsettings.json-Hinweis.</summary>
    internal static MagicValueClassification? ClassifyConnectionStringCandidate(string value)
    {
        foreach (var keyword in ConnectionStringKeywords)
        {
            if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return new MagicValueClassification(
                    true,
                    MagicValueCategory.ConfigCandidates,
                    "appsettings.json (ConnectionStrings-Sektion)",
                    $"Connection-String-Kandidat enthaelt '{keyword}'");
            }
        }
        return null;
    }

    /// <summary>Prueft, ob ein String-Literal eine URL ist (Prefix-Match gegen http/https/ftp).
    /// Liefert <see langword="null"/>, wenn kein Prefix matcht.</summary>
    internal static MagicValueClassification? ClassifyUrlCandidate(string value)
    {
        if (UrlPrefixes.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConfigCandidates,
                "appsettings.json (ApiSettings/BaseUrl o. ae.)",
                "URL-Literal");
        }
        return null;
    }

    /// <summary>Prueft, ob ein String-Literal ein identifier-artiger Header- oder
    /// Correlation-ID-Name mit Bindestrich ist (z. B. <c>"X-Correlation-ID"</c>).
    /// Liefert <see langword="null"/>, wenn der String nicht in das Muster passt.</summary>
    internal static MagicValueClassification? ClassifyHeaderIdentifierCandidate(string value)
    {
        if (value.Contains('-', StringComparison.Ordinal) && value.Length is > 2 and < 64
            && value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConstantCandidates,
                "Constants.cs (Header-/Identifier-Konstante)",
                "Identifier-artiger String mit Bindestrich");
        }
        return null;
    }

    /// <summary>Prueft, ob ein String-Literal einem Symbol-Namen im umschliessenden Scope
    /// entspricht (Parameter, lokale Variable, Member, Typ). Liefert eine
    /// <c>nameof_candidates</c>-Klassifizierung, wenn das Literal exakt dem
    /// Identifier-Namen entspricht.</summary>
    internal static MagicValueClassification? ClassifyNameofCandidate(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        var literalText = literal.Token.ValueText;
        if (string.IsNullOrEmpty(literalText)) return null;

        // Scope-Root: naechstes umschliessendes Method/Accessor/Local-Function-Body. Wenn
        // keines existiert (z. B. Literal in einem Field-Initializer), ist der Scope
        // null und wir ueberspringen die Suche — Field-Initializer koennen ohnehin keine
        // Magic-Value-Stringliterale enthalten, die per nameof() aufgeloest werden sollen.
        var scopeRoot = FindScopeRoot(literal);
        if (scopeRoot is null) return null;

        if (!HasMatchingSymbolName(scopeRoot, literalText)) return null;

        return new MagicValueClassification(
            true,
            MagicValueCategory.NameofCandidates,
            $"nameof({literalText})",
            "Name des Symbols im Scope");
    }

    /// <summary>Prueft, ob im umschliessenden Scope mindestens ein Symbol-Bezeichner
    /// (Identifier-Referenz oder Deklarations-Bezeichner) exakt dem Literal-Text
    /// entspricht. Aus <see cref="ClassifyNameofCandidate"/> extrahiert, um dessen
    /// kognitive Komplexitaet unter dem 12-Limit zu halten.</summary>
    private static bool HasMatchingSymbolName(SyntaxNode scopeRoot, string literalText)
    {
        return scopeRoot.DescendantNodesAndSelf()
            .Where(IsNameofCandidateNode)
            .Select(ExtractSymbolName)
            .Any(name => !string.IsNullOrEmpty(name)
                && string.Equals(name, literalText, StringComparison.Ordinal));
    }

    /// <summary>Filtert Syntax-Knoten, deren Identifier fuer die Nameof-Heuristik relevant
    /// ist. Beinhaltet sowohl Identifier-Referenzen (IdentifierNameSyntax) als auch
    /// Deklarations-Bezeichner (Parameter/Variable/Property/Method/Type/EnumMember).</summary>
    private static bool IsNameofCandidateNode(SyntaxNode n) =>
        n is IdentifierNameSyntax
            || n is ParameterSyntax
            || n is VariableDeclaratorSyntax
            || n is PropertyDeclarationSyntax
            || n is MethodDeclarationSyntax
            || n is TypeDeclarationSyntax
            || n is EnumMemberDeclarationSyntax;

    /// <summary>Extrahiert den relevanten Bezeichner-Text aus einem Syntax-Knoten fuer die
    /// Nameof-Heuristik. Liefert <see cref="string.Empty"/> fuer nicht-relevante Knoten.</summary>
    private static string ExtractSymbolName(SyntaxNode n) => n switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        ParameterSyntax p => p.Identifier.ValueText,
        VariableDeclaratorSyntax v => v.Identifier.ValueText,
        PropertyDeclarationSyntax p => p.Identifier.ValueText,
        MethodDeclarationSyntax m => m.Identifier.ValueText,
        TypeDeclarationSyntax t => t.Identifier.ValueText,
        EnumMemberDeclarationSyntax e => e.Identifier.ValueText,
        _ => string.Empty,
    };

    /// <summary>Prueft, ob ein String-Literal ein hartcodiertes Secret/Credential ist
    /// (CWE-798). Erkennung ueber drei orthogonale Heuristiken: Praefix-Muster (AWS Access
    /// Key, OpenAI, GitHub PAT, Slack), umgebender Symbol-Name (Parameter/Variable/Feld
    /// mit Secret-Name) und der Literal-Wert selbst (z. B. "password" als hartcodierter
    /// String-Parameter). Liefert in allen Faellen eine <c>security_candidates</c>-
    /// Klassifizierung mit hoeherer Prioritaet als <c>config_candidates</c>.</summary>
    internal static MagicValueClassification? ClassifySecurityCandidate(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        var literalText = literal.Token.ValueText;
        if (string.IsNullOrEmpty(literalText)) return null;

        // Heuristik 1: Praefix-Match (z. B. "AKIAIOSFODNN7EXAMPLE", "sk-...").
        if (SecurityPrefixes.Any(prefix => literalText.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.SecurityCandidates,
                "In Secret-Store/KeyVault auslagern",
                "Hartcodiertes Secret/Credential (CWE-798, AKIA/sk-/ghp_/xoxb-Praefix)");
        }

        // Heuristik 2: umgebender Symbol-Name deutet auf Secret/Token/Credential.
        var symbolName = ResolveSurroundingName(literal);
        if (symbolName is not null && SecurityNameKeywords.Any(k => symbolName.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.SecurityCandidates,
                "In Secret-Store/KeyVault auslagern",
                $"Hartcodiertes Secret/Credential (CWE-798, Symbol-Name '{symbolName}')");
        }

        // Heuristik 3: der Literal-Wert selbst entspricht exakt einem Security-Schluesselwort.
        // Z. B. Connect("password") wo "password" direkt als Argument-Wert ein Secret
        // andeutet. Bewusst auf exakte Gleichheit beschraenkt (OrdinalIgnoreCase),
        // um False Positives wie 'publicKeyToken' oder 'CancellationToken' auszuschliessen.
        if (SecurityNameKeywords.Contains(literalText))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.SecurityCandidates,
                "In Secret-Store/KeyVault auslagern",
                $"Hartcodiertes Secret/Credential (CWE-798, Wert entspricht '{literalText}')");
        }

        return null;
    }

    /// <summary>Prueft, ob ein numerisches Literal einer Well-known Buffer-Konstante
    /// entspricht (1024/2048/4096/8192) und im umgebenden Kontext einen entsprechenden
    /// Bezeichner (buffer/chunk/size) traegt.</summary>
    internal static MagicValueClassification? ClassifyStandardCandidateExtras(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        if (literal.Token.Value is not int value) return null;
        if (!StandardBufferNumbers.Contains(value)) return null;
        if (!HasBufferContext(literal, model)) return null;

        var name = StandardBufferNames.TryGetValue(value, out var mapped)
            ? mapped
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new MagicValueClassification(
            true,
            MagicValueCategory.StandardCandidates,
            $"NamedConstant ({name})",
            "Well-known Buffer-Groesse");
    }

    private static bool HasBufferContext(LiteralExpressionSyntax literal, SemanticModel? model)
    {
        if (model is not null && MagicValuesNumberClassifier.TryResolveParameterName(literal, model) is { } paramName)
        {
            if (IsBufferIdentifier(paramName)) return true;
        }

        var surroundingName = ResolveSurroundingName(literal);
        if (surroundingName is not null && IsBufferIdentifier(surroundingName))
        {
            return true;
        }

        return false;
    }

    private static bool IsBufferIdentifier(string name)
    {
        return name.Contains("buffer", StringComparison.OrdinalIgnoreCase)
            || name.Contains("chunk", StringComparison.OrdinalIgnoreCase)
            || name.Contains("size", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Prueft, ob ein String-Literal als User-Facing Exception-Message fungiert.
    /// Pragmatische Variante: das Literal ist Argument in einem Exception-Konstruktor
    /// UND die effektive String-Laenge (ohne Whitespace) ueberschreitet 15 Zeichen.
    /// UI-Prompts/Logins waeren zusaetzliche Caller-Type-Heuristiken, die als
    /// Tech-Debt offen sind.</summary>
    internal static MagicValueClassification? ClassifyLocalizationCandidate(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        var literalText = literal.Token.ValueText;
        if (string.IsNullOrEmpty(literalText)) return null;

        // Schwellwert: < 16 Zeichen (Whitespace ungleich) gelten als technische Marker
        // (z. B. "Not Found", "Bad Request") und werden NICHT als Lokalisierungs-Kandidat
        // gemeldet — die wuerden nur die False-Positive-Quote hochtreiben.
        var effectiveLength = literalText.Count(c => !char.IsWhiteSpace(c));
        if (effectiveLength <= 15) return null;

        // Argument in Exception-Konstruktor: literal.Parent ist ArgumentSyntax,
        // grandparent ist BaseArgumentListSyntax, great-grandparent ist
        // ObjectCreationExpressionSyntax mit Exception-typischem Identifier.
        if (literal.Parent is not ArgumentSyntax argument) return null;
        if (argument.Parent is not BaseArgumentListSyntax argList) return null;
        if (argList.Parent is not ObjectCreationExpressionSyntax objectCreation) return null;
        if (!ExceptionTypeNames.Contains(objectCreation.Type.ToString())) return null;

        return new MagicValueClassification(
            true,
            MagicValueCategory.LocalizationCandidates,
            "IStringLocalizer / .resx",
            $"User-Facing Exception-Message ({effectiveLength} Zeichen, > 15)");
    }

    /// <summary>Findet den naechsten umschliessenden Methoden-/Accessor-/Konstruktor-Body
    /// fuer den <c>nameof_candidates</c>-Scope-Walk. Bricht an Type/Member-Grenzen ab,
    /// damit Member-Name-Kollisionen ueber Klassengrenzen hinweg nicht falsch-positive
    /// Treffer erzeugen.</summary>
    private static SyntaxNode? FindScopeRoot(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BaseMethodDeclarationSyntax
                or AccessorDeclarationSyntax
                or LocalFunctionStatementSyntax
                or AnonymousFunctionExpressionSyntax)
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Loest den umgebenden Symbol-Namen auf (Parameter, Variable, Feld, Property, Argument mit
    /// NameColon). Liefert <see langword="null"/>, wenn keiner der Kontexte zutrifft.</summary>
    private static string? ResolveSurroundingName(LiteralExpressionSyntax literal)
    {
        // Argument-Name: z. B. Connect(connectionString: "...")
        if (literal.Parent is ArgumentSyntax { NameColon: not null } namedArg)
        {
            return namedArg.NameColon!.Name.Identifier.ValueText;
        }

        // Variable-Declarator / Property / Parameter
        for (var current = literal.Parent; current is not null; current = current.Parent)
        {
            if (current is VariableDeclaratorSyntax declarator)
            {
                return declarator.Identifier.ValueText;
            }

            if (current is ParameterSyntax parameter)
            {
                return parameter.Identifier.ValueText;
            }

            if (current is PropertyDeclarationSyntax property)
            {
                return property.Identifier.ValueText;
            }
        }

        return null;
    }
}
