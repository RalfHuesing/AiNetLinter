#nullable enable

using System.Collections.Generic;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

internal sealed class TestLintConsole : ILintConsole
{
    public List<string> Output { get; } = new();
    public List<string> Errors { get; } = new();

    public string OutputText => string.Join("\n", Output);
    public string ErrorText => string.Join("\n", Errors);
    public string Error => ErrorText;

    public void WriteLine(string message) => Output.Add(message);
    public void WriteError(string message) => Errors.Add(message);
}
