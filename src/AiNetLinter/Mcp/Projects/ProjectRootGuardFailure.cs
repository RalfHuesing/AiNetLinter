#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed record ProjectRootGuardFailure(string Code, string Message, string Hint);
