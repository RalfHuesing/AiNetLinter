#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Tests.Fixtures;

/// <summary>
/// Test-Fake: wirft beim Textzugriff eine <see cref="InvalidOperationException"/>, um eine echte
/// LinterEngine-Malfunction deterministisch zu simulieren (statt auf einen fragilen realen Timing-Race
/// zu warten, in dem eine Quelldatei zwischen Indexierung und Analyse vom Dateisystem verschwindet).
/// Bewusst kein IOException/UnauthorizedAccessException: Roslyns <c>TextDocumentState</c> faengt diese
/// beiden Typen intern ab (Workspace-Resilienz gegen verschwundene Quelldateien) und ersetzt sie durch
/// leeren Text statt die Exception zu propagieren — das wuerde den Malfunction-Fall zum Erfolgsfall
/// machen (empirisch verifiziert). Ein unspezifischer Exception-Typ hat keine solche Sonderbehandlung.
/// Verwendet ueber <see cref="AiNetLinter.Tests.TestHelper.CreateFaultySolution"/> — zentral statt in
/// mehreren MCP-Tool-Testklassen dupliziert.
/// </summary>
public sealed class ThrowingTextLoader : TextLoader
{
    public override Task<TextAndVersion> LoadTextAndVersionAsync(
        LoadTextOptions options, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Simulierter Lesefehler fuer Malfunction-Regressionstest.");
    }
}
