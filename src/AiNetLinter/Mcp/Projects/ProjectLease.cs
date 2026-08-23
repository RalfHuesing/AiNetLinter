#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed class ProjectLease(
    string rootPath,
    ProjectDefinition definition,
    McpCodeGraphServer server,
    Action release) : IDisposable
{
    private int released;

    public McpCodeGraphServer Server { get; } = server;

    internal string RootPath { get; } = rootPath;

    internal ProjectDefinition Definition { get; } = definition;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref released, 1, 0) != 0)
        {
            return;
        }

        release();
    }
}
