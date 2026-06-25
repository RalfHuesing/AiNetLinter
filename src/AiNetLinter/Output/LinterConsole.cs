#nullable enable

using System;

namespace AiNetLinter.Output;

internal sealed class LinterConsole : ILintConsole
{
    internal static readonly LinterConsole Instance = new();

    private LinterConsole() { }

    public void WriteLine(string message) => Console.WriteLine(message);
    public void WriteError(string message) => Console.Error.WriteLine(message);
}
