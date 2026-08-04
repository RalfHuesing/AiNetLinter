#nullable enable

using System;

namespace AiNetLinter.Output;

/// <summary>
/// MCP-spezifische <see cref="ILintConsole"/>-Implementierung, die <c>WriteLine</c> zwingend
/// nach <c>stderr</c> umleitet. Hintergrund: im MCP-Server-Modus ist <c>stdout</c> der
/// Transport-Kanal des JSON-RPC-Protokolls - ein einziger <c>Console.WriteLine</c>-Call
/// aus einer wiederverwendeten CLI-Klasse wuerde das Framing der gesamten Session
/// zerstoeren. Diese Implementierung macht den Schutz strukturell, nicht ueber Disziplin.
/// Singleton-Pattern analog <see cref="LinterConsole"/>.
/// </summary>
internal sealed class McpLintConsole : ILintConsole
{
    internal static readonly McpLintConsole Instance = new();

    private McpLintConsole() { }

    public void WriteLine(string message) => Console.Error.WriteLine(message);
    public void WriteError(string message) => Console.Error.WriteLine(message);
}
