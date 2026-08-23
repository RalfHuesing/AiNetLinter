#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed class ProjectLease(
    string rootPath,
    ProjectDefinition definition,
    McpCodeGraphServer server,
    Action<ProjectLease> release) : IDisposable
{
    private int released;
    private int loadFailedResponseEmitted;

    public McpCodeGraphServer Server { get; } = server;

    internal string RootPath { get; } = rootPath;

    internal ProjectDefinition Definition { get; } = definition;

    internal bool LoadFailedResponseEmitted =>
        Volatile.Read(ref loadFailedResponseEmitted) == 1;

    internal void MarkLoadFailedResponseEmitted()
    {
        Interlocked.Exchange(ref loadFailedResponseEmitted, 1);
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref released, 1, 0) != 0)
        {
            return;
        }

        release(this);
    }
}
