#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed record ProjectLoadFailure(string Message, string Context, string Hint);
