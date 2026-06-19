#nullable enable

namespace AiNetLinter.Output;

internal interface ILintConsole
{
    void WriteLine(string message);
    void WriteError(string message);
}
