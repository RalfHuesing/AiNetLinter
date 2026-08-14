#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Ergebnis der Heuristik-Klassifizierung fuer ein einzelnes Literal. <see cref="IsMagic"/> ist
/// <see langword="false"/> fuer Trivial-/Attribut-/Index-/Loop-/GetHashCode-Filterungen; das
/// Literal wird dann nicht in den Report aufgenommen.
/// </summary>
internal sealed record MagicValueClassification(
    bool IsMagic,
    MagicValueCategory Category,
    string Recommendation,
    string ContextHint);

/// <summary>
/// Parameter-Bundel fuer <see cref="MagicValuesClassifier.Classify"/>. Fasst die zwei
/// EPIC-2-Platzhalter-Bools (<c>includeTests</c>, <c>includeSuppressed</c>) zu einem
/// <c>ClassifierOptions</c>-Record zusammen, damit das Methoden-Signatur-Limit (max 4
/// Parameter) eingehalten wird.
/// </summary>
internal sealed record MagicValueClassifierOptions(
    bool IncludeTests,
    bool IncludeSuppressed);

/// <summary>
/// Reine, deterministische Heuristik-Funktion: bestimmt, ob ein Literal ein "Magic Value" im
/// Sinn von <c>find_magic_values</c> ist. Bewusst konservativ (mehr False Negatives als False
/// Positives) — siehe Konzept §"Rausch-Filterung". Syntaktische Pruefungen dominieren; ein
/// optionaler <see cref="SemanticModel"/> wird nur fuer Aufruf-Argument-Kontext (z. B.
/// <c>Thread.Sleep(5000)</c> → <c>millisecondsTimeout</c>-Parameter) herangezogen, sonst reiner
/// AST. Unit-testbar ohne Roslyn-Solution: nur <see cref="LiteralExpressionSyntax"/> plus
/// statische Kontext-Hints erforderlich.
/// </summary>
internal static class MagicValuesClassifier
{
    // Trivial-Werte, die nie gemeldet werden (Konzept §"Rausch-Filterung" Punkt 1).
    // Bewusst klein gehalten: '0'/'1'/'-1' decken haeufige Index-/Loop-Startwerte ab,
    // Leerstring/' '/'\n' decken Empty-/Whitespace-Literale ab, 'true'/'false'/'null'
    // sind Bool-/Null-Literale, die nie eine Refactoring-Empfehlung wert sind.
    private static readonly HashSet<string> TrivialStringLiterals = new(StringComparer.Ordinal)
    {
        string.Empty,
        " ",
        "\n",
    };

    private const int TrivialNumberLow = -1;
    private const int TrivialNumberHigh = 1;

    // Schlüsselwörter im Literal, die auf einen Connection-String hindeuten
    // (Konzept §"Wie" Punkt 4 — "Server=", "Database=", ...).
    private static readonly string[] ConnectionStringKeywords =
    [
        "Server=", "Database=", "Trusted_Connection=", "User Id=", "Password=", "Data Source=",
    ];

    /// <summary>
    /// Klassifiziert ein einzelnes <paramref name="literal"/>. Liefert <c>IsMagic=false</c>
    /// fuer Trivial-/Attribut-/Index-/Loop-/GetHashCode-Faelle, sonst eine fachliche
    /// Kategorie + Empfehlung + Kontext-Hinweis.
    /// </summary>
    /// <param name="literal">Das zu pruefende Literal. Raw String Literals sind ebenfalls
    /// <see cref="LiteralExpressionSyntax"/> (Kind <c>StringLiteralExpression</c>/<c>Utf8StringLiteralExpression</c>).</param>
    /// <param name="model">Optional — nur fuer Aufruf-Argument-Kontext (z. B. <c>Thread.Sleep(5000)</c>)
    /// verwendet. <see langword="null"/> in syntaktischen Unit-Tests.</param>
    /// <param name="ignoreNumbers">Zusaetzliche Zahlen, die ueber die Trivial-Liste hinaus
    /// ignoriert werden sollen (z. B. 24/60/360/1000 fuer Zeit-Konstanten).</param>
    /// <param name="options">EPIC-2-Platzhalter-Bools gebuendelt; aktuell No-op in EPIC-1,
    /// aber im Args-Record bereits durchgereicht fuer API-Stabilitaet.</param>
    internal static MagicValueClassification Classify(
        LiteralExpressionSyntax literal,
        SemanticModel? model,
        IReadOnlySet<int> ignoreNumbers,
        MagicValueClassifierOptions options)
    {
        _ = options; // EPIC-1 No-op: includeTests/includeSuppressed sind Platzhalter

        // Attribut-Isolierung: jedes Literal innerhalb eines Attributs (z. B. [Route("/api/v1")])
        // ist semantisch vom Compiler-/Framework-Vertrag abhaengig und nicht refactorbar — nie melden.
        if (literal.FirstAncestorOrSelf<AttributeSyntax>() is not null)
        {
            return NotMagic();
        }

        // GetHashCode-Sonderfall: Literale innerhalb eines GetHashCode-Overrides (typischerweise
        // Primzahlen 17/23/31 in 'hash = hash * 31 + ...') sind idiomatisches Boilerplate, nicht
        // Magic Values. Greift sowohl fuer eigene als auch fuer override-Methoden.
        if (IsInsideGetHashCode(literal))
        {
            return NotMagic();
        }

        // Index/Loop-Ausnahme: Literale in Array-Index-Zugriffen (args[2]) oder als
        // Schleifenzähler-Initialisierung (for (int i = 2; ...)) sind strukturelle Navigation,
        // keine fachlichen Werte.
        if (IsIndexLiteral(literal) || IsLoopInitializer(literal))
        {
            return NotMagic();
        }

        // Trivial-Filter.
        if (IsTrivialLiteral(literal, ignoreNumbers))
        {
            return NotMagic();
        }

        // Eigentliche Heuristik: Zahl oder String.
        return ClassifyNonTrivial(literal, model);
    }

    private static MagicValueClassification NotMagic() =>
        new(false, MagicValueCategory.ConfigCandidates, string.Empty, string.Empty);

    private static bool IsTrivialLiteral(LiteralExpressionSyntax literal, IReadOnlySet<int> ignoreNumbers)
    {
        switch (literal.Kind())
        {
            case SyntaxKind.StringLiteralExpression:
            case SyntaxKind.Utf8StringLiteralExpression:
            case SyntaxKind.CharacterLiteralExpression:
            {
                var text = literal.Token.ValueText;
                return TrivialStringLiterals.Contains(text);
            }
            case SyntaxKind.NumericLiteralExpression:
            {
                if (literal.Token.Value is int i)
                {
                    if (i >= TrivialNumberLow && i <= TrivialNumberHigh) return true;
                    return ignoreNumbers.Contains(i);
                }
                // Andere numerische Typen (double/float/decimal/long) sind in EPIC-1 nicht trivial —
                // Schwellenwert-Heuristik behandelt sie als constant_candidates.
                return false;
            }
            case SyntaxKind.TrueLiteralExpression:
            case SyntaxKind.FalseLiteralExpression:
            case SyntaxKind.NullLiteralExpression:
                return true;
            default:
                return false;
        }
    }

    private static bool IsIndexLiteral(LiteralExpressionSyntax literal)
    {
        // Array-/Indexer-Zugriff: literal.Parent == ElementAccessExpressionSyntax und literal
        // ist in dessen ArgumentList enthalten. Tuple-ElementAccess (.Item3) hat kein Literal
        // als Parent, daher nicht abgedeckt — Tuple-Element-Namen sind ohnehin keine Magic Values.
        if (literal.Parent is ArgumentSyntax argument
            && argument.Parent is BaseArgumentListSyntax
            && argument.Parent.Parent is ElementAccessExpressionSyntax)
        {
            return true;
        }

        // Range-Index (z. B. arr[^2]): implizit ueber ArgumentList abgedeckt, daher nicht
        // separat noetig. '^2' ist syntaktisch ein PrefixUnaryExpression, das literal enthaelt.
        return false;
    }

    private static bool IsLoopInitializer(LiteralExpressionSyntax literal)
    {
        // for (int i = 2; ...): literal ist direkt in einer VariableDeclarator-Initializer-Kette
        // unter einem ForStatement. detection: ForStatementSyntax -> Declaration -> Variables ->
        // VariableDeclarator -> Initializer -> EqualsValueClause -> literal.
        for (var current = literal.Parent; current is not null; current = current.Parent)
        {
            if (current is ForStatementSyntax)
            {
                return true;
            }

            // Nicht weiter hoch als bis zur Methode (sonst waeren Variablen-Initialisierungen
            // ebenfalls 'loop' — was sie nicht sind).
            if (current is MethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static bool IsInsideGetHashCode(LiteralExpressionSyntax literal)
    {
        for (var current = literal.Parent; current is not null; current = current.Parent)
        {
            if (current is MethodDeclarationSyntax method
                && string.Equals(method.Identifier.Text, "GetHashCode", StringComparison.Ordinal))
            {
                return true;
            }

            if (current is AccessorDeclarationSyntax or LocalFunctionStatementSyntax)
            {
                return false;
            }
        }

        return false;
    }

    private static MagicValueClassification ClassifyNonTrivial(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        return literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression or SyntaxKind.Utf8StringLiteralExpression
                => ClassifyString(literal.Token.ValueText, literal, model),
            SyntaxKind.NumericLiteralExpression
                => MagicValuesNumberClassifier.ClassifyNumber(literal, model),
            _ => NotMagic(),
        };
    }

    private static MagicValueClassification ClassifyString(
        string value,
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        // Connection-String-Heuristik: jedes Schluesselwort-Treffer genuegt — Server- und Database-Keys
        // sind praktisch immer Teil eines Connection-Strings, nicht eigenstaendige Magic Values.
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

        // URL-Heuristik: 'http://' / 'https://' / 'ftp://' sind sehr starke Indikatoren fuer
        // eine Konfigurations-URL. Bewusst einfach (kein UrlParser), um false positives in
        // Kommentaren / XML-Doku zu vermeiden — letztere beginnen nie mit einem Schema.
        if (StartsWithAny(value, "http://", "https://", "ftp://"))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConfigCandidates,
                "appsettings.json (ApiSettings/BaseUrl o. ae.)",
                "URL-Literal");
        }

        // Windows-Pfad-Heuristik: 'C:\...' oder '\\server\share' sind Pfad-Literale.
        if (LooksLikeWindowsPath(value))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConfigCandidates,
                "appsettings.json (Paths-Sektion)",
                "Windows-Pfad-Literal");
        }

        // Format-String-Heuristik: typische .NET-Format-String-Patterns (Datum, Zahlen, Platzhalter).
        if (LooksLikeFormatString(value))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConstantCandidates,
                "Constants.cs (Format-String-Konstante)",
                "Format-String-Literal");
        }

        // Header-/Correlation-ID-Name mit Bindestrich (z. B. 'X-Correlation-ID'): semantisch
        // ein Identifier, sollte zentral definiert werden.
        if (value.Contains('-', StringComparison.Ordinal) && value.Length is > 2 and < 64
            && value.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConstantCandidates,
                "Constants.cs (Header-/Identifier-Konstante)",
                "Identifier-artiger String mit Bindestrich");
        }

        return NotMagic();
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeWindowsPath(string value)
    {
        if (value.Length < 3) return false;
        // 'C:\' oder 'C:/' (Laufwerksbuchstabe + Doppelpunkt + Separator).
        if (char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'))
        {
            return true;
        }
        // UNC '\\server\share'.
        if (value.Length >= 2 && value[0] == '\\' && value[1] == '\\')
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeFormatString(string value)
    {
        if (value.Length < 3) return false;
        // Datumspatterns (z. B. 'yyyy-MM-dd', 'HH:mm:ss'): min. 2 Buchstaben aus y/M/d/H/m/s.
        if (value.Contains("yyyy", StringComparison.Ordinal)
            || value.Contains("MM", StringComparison.Ordinal)
            || value.Contains("dd", StringComparison.Ordinal)
            || value.Contains("HH", StringComparison.Ordinal)
            || value.Contains("mm", StringComparison.Ordinal)
            || value.Contains("ss", StringComparison.Ordinal))
        {
            return true;
        }

        // Numerische Format-Patterns ('{0:F2}', 'N2', 'C', ...).
        if (value.StartsWith("{0", StringComparison.Ordinal) && value.Contains('}', StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }
}
