#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// String- und Number-spezifische Sub-Heuristiken fuer <see cref="MagicValuesClassifier"/>,
/// die in EPIC-2 nachgereicht wurden: <c>nameof_candidates</c>, <c>security_candidates</c>,
/// <c>localization_candidates</c> und die <c>standard_candidates</c>-Erweiterung um
/// nicht-HTTP Magic Numbers. Aus den Hauptdateien in eine eigene Datei extrahiert, damit
/// <see cref="MagicValuesClassifier"/> und <see cref="MagicValuesNumberClassifier"/> unter
/// dem <c>MaxLineCount: 500</c>-Limit bleiben (siehe <c>AiNetLinter.mdc</c>).
/// </summary>
internal static class MagicValuesStringHeuristics
{
    // Parameternamen, die auf ein Secret/Credential hindeuten
    // (Konzept §"Muss-Haven" Punkt 6 — CWE-798).
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
    // (Konzept §"Wie" Punkt 4 — "Server=", "Database=", ...).
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

    // Well-known Buffer- und Zeit-Konstanten, die zwar nicht HTTP-Statuscodes sind, aber
    // semantisch eindeutig (1024 = 1 KiB Buffer, 1000 = ms/Second, 86400 = s/Tag).
    private static readonly HashSet<int> StandardExtraNumbers = new()
    {
        1024, 2048, 4096, 8192, 1000, 24, 60, 360, 1440, 86400,
    };

    // Empfehlungs-Mapping fuer die StandardExtraNumbers — wird im Recommendation-String
    // verwendet, damit der Refactor-Hint lesbar bleibt (BufferSize vs. Time-Konstante).
    private static readonly Dictionary<int, string> StandardExtraNames = new()
    {
        [1024] = "BufferSize (1 KiB)",
        [2048] = "BufferSize (2 KiB)",
        [4096] = "BufferSize (4 KiB)",
        [8192] = "BufferSize (8 KiB)",
        [1000] = "MillisecondsPerSecond",
        [24] = "HoursPerDay",
        [60] = "SecondsPerMinute",
        [360] = "SecondsPerHour",
        [1440] = "MinutesPerDay",
        [86400] = "SecondsPerDay",
    };

    // Exception-Typen, die als Heuristik fuer User-Facing-Message-Texte gelten
    // (Konzept §"Muss-Haven" Punkt "Lokalisierungs-Kandidaten" — pragmatische Variante).
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
    /// Identifier-Namen entspricht — siehe Konzept §"Muss-Haven" Punkt 4.</summary>
    internal static MagicValueClassification? ClassifyNameofCandidate(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        var literalText = literal.Token.ValueText;
        if (string.IsNullOrEmpty(literalText)) return null;

        // 1) Parameter-Namen im umschliessenden Method/Constructor-Scope pruefen.
        // Parameter-Namen sind nicht als IdentifierNameSyntax im Tree vorhanden — sie
        // muessen separat ueber ParameterSyntax-Aufzaehlung gefunden werden.
        var scopeRoot = FindScopeRoot(literal);
        if (scopeRoot is not null)
        {
            var parameterMatch = scopeRoot.DescendantNodesAndSelf()
                .OfType<ParameterSyntax>()
                .Any(p => string.Equals(p.Identifier.ValueText, literalText, StringComparison.Ordinal));
            if (parameterMatch) return new MagicValueClassification(
                true,
                MagicValueCategory.NameofCandidates,
                $"nameof({literalText})",
                "Name des Symbols im Scope");
        }

        // 2) IdentifierNameSyntax (Member-/Variable-Referenzen) im Scope pruefen.
        if (scopeRoot is not null)
        {
            var identifierMatch = scopeRoot.DescendantNodesAndSelf()
                .OfType<IdentifierNameSyntax>()
                .Any(id => string.Equals(id.Identifier.ValueText, literalText, StringComparison.Ordinal));
            if (identifierMatch) return new MagicValueClassification(
                true,
                MagicValueCategory.NameofCandidates,
                $"nameof({literalText})",
                "Name des Symbols im Scope");
        }

        return null;
    }

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

        // Heuristik 3: der Literal-Wert selbst entspricht einem Security-Schluesselwort.
        // Z. B. Connect("password") wo "password" direkt als Argument-Wert ein Secret
        // andeutet. Bewusst eng gefasst (nur exakte Substring-Matches, keine Fuzzy-Suche),
        // um die False-Positive-Quote niedrig zu halten.
        if (SecurityNameKeywords.Any(k => literalText.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.SecurityCandidates,
                "In Secret-Store/KeyVault auslagern",
                $"Hartcodiertes Secret/Credential (CWE-798, Wert enthaelt '{literalText}')");
        }

        return null;
    }

    /// <summary>Prueft, ob ein numerisches Literal einer Well-known Buffer- oder Zeit-Konstante
    /// entspricht. Wird in <see cref="MagicValuesNumberClassifier.ClassifyNumber"/> am Ende
    /// aufgerufen, wenn weder HTTP-Statuscode noch Timeout-Parameter noch Schwellenwert greifen.
    /// Liefert <see langword="null"/>, wenn das Literal keine Standard-Konstante ist — dann
    /// faellt der Aufrufer auf <c>NotMagic</c> zurueck.</summary>
    internal static MagicValueClassification? ClassifyStandardCandidateExtras(
        LiteralExpressionSyntax literal)
    {
        if (literal.Token.Value is not int value) return null;
        if (!StandardExtraNumbers.Contains(value)) return null;

        var name = StandardExtraNames.TryGetValue(value, out var mapped)
            ? mapped
            : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return new MagicValueClassification(
            true,
            MagicValueCategory.StandardCandidates,
            $"NamedConstant ({name})",
            "Well-known Konstante (Buffer-Groesse / Zeit-Konstante)");
    }

    /// <summary>Prueft, ob ein String-Literal als User-Facing Exception-Message fungiert
    /// (Konzept §"Muss-Haven" Punkt "Lokalisierungs-Kandidaten"). Pragmatische Variante:
    /// das Literal ist Argument in einem Exception-Konstruktor UND die effektive
    /// String-Laenge (ohne Whitespace) ueberschreitet 15 Zeichen. UI-Prompts/Logins
    /// waeren zusaetzliche Caller-Type-Heuristiken, die als Tech-Debt offen sind.</summary>
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

    /// <summary>Loest den umgebenden Symbol-Namen auf (Parameter, Variable, Feld, Argument mit
    /// NameColon). Liefert <see langword="null"/>, wenn keiner der Kontexte zutrifft.</summary>
    private static string? ResolveSurroundingName(LiteralExpressionSyntax literal)
    {
        // Argument-Name: z. B. Connect(connectionString: "...")
        if (literal.Parent is ArgumentSyntax { NameColon: not null } namedArg)
        {
            return namedArg.NameColon!.Name.Identifier.ValueText;
        }

        // Variable-Declarator: var password = "..."
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
        }

        return null;
    }
}
