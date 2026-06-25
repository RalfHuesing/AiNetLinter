#nullable enable

using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Maps;

internal sealed class TestLintConsole : ILintConsole
{
    private readonly StringBuilder _sb = new();
    private readonly StringBuilder _errSb = new();

    public string Output => _sb.ToString();
    public string Error => _errSb.ToString();

    public void WriteLine(string message) => _sb.AppendLine(message);
    public void WriteError(string message) => _errSb.AppendLine(message);
}
