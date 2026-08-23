#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed class ProjectLease(McpCodeGraphServer server, Action release) : IDisposable
{
    private int released;

    public McpCodeGraphServer Server { get; } = server;

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref released, 1, 0) != 0)
        {
            return;
        }

        release();
    }
}
