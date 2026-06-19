#nullable enable

using System;

namespace AiNetLinter.Output;

internal sealed class ConsoleLintConsole : ILintConsole
{
    internal static readonly ConsoleLintConsole Instance = new();

    private ConsoleLintConsole() { }

    public void WriteLine(string message) => Console.WriteLine(message);
    public void WriteError(string message) => Console.Error.WriteLine(message);
}
