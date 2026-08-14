#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AiNetLinter.Mcp.Tools.MagicValues;

/// <summary>
/// Number-spezifische Heuristik-Sub-Routinen fuer <see cref="MagicValuesClassifier"/>:
/// HTTP-Statuscodes, Timeout-Parameter-Kontext (via <see cref="SemanticModel"/>) und
/// Schwellenwert-Konstanten in <c>const</c>/<c>readonly</c>/<c>static</c>-Feldern.
/// Aus der Hauptklasse in eine eigene Datei extrahiert, damit <see cref="MagicValuesClassifier"/>
/// unter dem <c>MaxLineCount: 500</c>-Limit bleibt (siehe <c>AiNetLinter.mdc</c>).
/// </summary>
internal static class MagicValuesNumberClassifier
{
    // Parameternamen, die auf einen Timeout / Delay / Retry hindeuten
    // (Konzept §"Wie" Punkt 4 — "Thread.Sleep(5000)" etc.).
    internal static readonly System.Collections.Generic.HashSet<string> TimeoutParameterNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "timeout", "millisecondsTimeout", "delay", "retryCount", "maxRetries", "port", "bufferSize",
        };

    /// <summary>Klassifiziert ein numerisches Literal — HTTP-Statuscode, Timeout-Argument
    /// oder Schwellenwert-Konstante. Liefert <c>IsMagic=false</c>, wenn keiner der Pfade
    /// greift.</summary>
    internal static MagicValueClassification ClassifyNumber(
        LiteralExpressionSyntax literal,
        SemanticModel? model)
    {
        // HTTP-Statuscode-Heuristik: 1xx/2xx/3xx/4xx/5xx in jedem nicht-trivialen Kontext.
        // Der Wertebereich 100-599 ist sehr eng und semantisch eindeutig — selbst in einem
        // Vergleichsausdruck (if (status == 404)) ist die Empfehlung StatusCodes.Status... wertvoll.
        if (literal.Token.Value is int statusCode && IsHttpStatusCode(statusCode))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.StandardCandidates,
                $"StatusCodes.Status{statusCode}{(statusCode >= 500 ? "InternalServerError" : ResolveStatusCodeName(statusCode))}",
                "HTTP-Statuscode-Literal");
        }

        // Aufruf-Argument-Kontext: Literal in einem Methodenaufruf, dessen semantisch aufgeloester
        // Parameter einen Timeout- / Delay- / Retry-Namen hat.
        if (IsInMethodCallArgument(literal)
            && model is not null
            && TryResolveParameterName(literal, model) is { } paramName
            && TimeoutParameterNames.Contains(paramName))
        {
            return new MagicValueClassification(
                true,
                MagicValueCategory.ConfigCandidates,
                $"appsettings.json ({paramName}-Eintrag)",
                $"Timeout/Delay-Parameter '{paramName}'");
        }

        // Schwellenwert-Heuristik: doubles/floats in const-/readonly-/static-Feld-Initialisierern
        // sind typischerweise Magic Thresholds (0.5, 0.19, 0.80) — sollen zentral definiert werden.
        if (literal.Token.Value is double or float or decimal)
        {
            if (IsInConstFieldInitializer(literal))
            {
                return new MagicValueClassification(
                    true,
                    MagicValueCategory.ConstantCandidates,
                    "Constants.cs (zentrale Schwellenwert-Konstante)",
                    "Schwellenwert-Literal in const-Feld");
            }
        }

        return new MagicValueClassification(false, MagicValueCategory.ConfigCandidates, string.Empty, string.Empty);
    }

    internal static bool IsHttpStatusCode(int value)
    {
        if (value is < 100 or > 599) return false;
        return (value / 100) is >= 1 and <= 5;
    }

    internal static string ResolveStatusCodeName(int code) => code switch
    {
        200 => "OK",
        201 => "Created",
        204 => "NoContent",
        301 => "MovedPermanently",
        302 => "Found",
        304 => "NotModified",
        400 => "BadRequest",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "NotFound",
        409 => "Conflict",
        422 => "UnprocessableEntity",
        429 => "TooManyRequests",
        500 => "InternalServerError",
        502 => "BadGateway",
        503 => "ServiceUnavailable",
        504 => "GatewayTimeout",
        _ => code.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    internal static bool IsInMethodCallArgument(LiteralExpressionSyntax literal)
    {
        return literal.Parent is ArgumentSyntax argument
            && argument.Parent is BaseArgumentListSyntax;
    }

    internal static bool IsInConstFieldInitializer(LiteralExpressionSyntax literal)
    {
        // const X = 5; readonly X = 5; static X = 5; — nur Field-Initializer zaehlen,
        // lokale Variablen-Initialisierungen nicht.
        for (var current = literal.Parent; current is not null; current = current.Parent)
        {
            if (current is VariableDeclaratorSyntax declarator)
            {
                return IsFieldWithConstLikeModifier(declarator);
            }

            if (current is LocalDeclarationStatementSyntax or VariableDeclarationSyntax)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Prueft, ob der <see cref="VariableDeclaratorSyntax"/> in einem Field mit
    /// const/readonly/static-Modifier liegt. Aus <see cref="IsInConstFieldInitializer"/>
    /// extrahiert, um dessen kognitive Komplexitaet unter dem 15-Limit zu halten.</summary>
    private static bool IsFieldWithConstLikeModifier(VariableDeclaratorSyntax declarator)
    {
        var fieldDecl = declarator.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (fieldDecl is null) return false;
        return fieldDecl.Modifiers.Any(m =>
            m.IsKind(SyntaxKind.ConstKeyword)
            || m.IsKind(SyntaxKind.StaticKeyword)
            || m.IsKind(SyntaxKind.ReadOnlyKeyword));
    }

    internal static string? TryResolveParameterName(LiteralExpressionSyntax literal, SemanticModel model)
    {
        if (literal.Parent is not ArgumentSyntax argument) return null;
        if (argument.Parent is not BaseArgumentListSyntax argList) return null;

        // Argument-Index in der ArgumentList bestimmen.
        var argIndex = argList.Arguments.IndexOf(argument);
        if (argIndex < 0) return null;

        // Die umschliessende Invocation/ObjectCreation aufrufen — via Parent-Walk, weil
        // GetSymbolInfo auf Argument keinen direkten Parameter liefert (es liefert die
        // aufgerufene Methode).
        SyntaxNode? invocation = argList.Parent as InvocationExpressionSyntax;
        invocation ??= argList.Parent as ObjectCreationExpressionSyntax;

        if (invocation is null) return null;

        var symbolInfo = model.GetSymbolInfo(invocation);
        if (symbolInfo.Symbol is not IMethodSymbol method) return null;

        // Argumente koennen benannt sein — dann NameOrArgIndex verwenden.
        if (argument.NameColon is not null && argument.NameColon.Name.Identifier.Value is string namedArg)
        {
            foreach (var p in method.Parameters)
            {
                if (string.Equals(p.Name, namedArg, StringComparison.Ordinal))
                {
                    return p.Name;
                }
            }
        }

        if (argIndex < method.Parameters.Length)
        {
            return method.Parameters[argIndex].Name;
        }

        return null;
    }
}
