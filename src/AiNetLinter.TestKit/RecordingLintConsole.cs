#nullable enable

using System.Collections.Generic;
using AiNetLinter.Output;

namespace AiNetLinter.TestKit;

internal sealed class RecordingLintConsole : ILintConsole
{
    private readonly List<string> outputLines = [];
    private readonly List<string> errorLines = [];

    public IReadOnlyList<string> OutputLines => outputLines;
    public IReadOnlyList<string> ErrorLines => errorLines;
    public string OutputText => string.Join("\n", outputLines);
    public string ErrorText => string.Join("\n", errorLines);

    public void WriteLine(string message) => outputLines.Add(message);
    public void WriteError(string message) => errorLines.Add(message);
}
