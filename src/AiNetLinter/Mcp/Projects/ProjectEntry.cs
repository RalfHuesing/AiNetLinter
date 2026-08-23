#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed class ProjectEntry(string rootPath, ProjectDefinition definition, McpCodeGraphServer server, DateTime lastUsedUtc)
{
    private int inFlightCount;

    internal string RootPath { get; } = rootPath;

    internal ProjectDefinition Definition { get; } = definition;

    internal McpCodeGraphServer Server { get; } = server;

    // Wird nur unter dem Registry-Lock geschrieben und gelesen.
    internal DateTime LastUsedUtc { get; set; } = lastUsedUtc;

    // Wird nur unter dem Registry-Lock geschrieben und gelesen.
    internal bool PendingEviction { get; set; }

    internal int InFlightCount => Interlocked.CompareExchange(ref inFlightCount, 0, 0);

    internal ProjectLease OpenLease()
    {
        Interlocked.Increment(ref inFlightCount);
        return new ProjectLease(Server, () => Interlocked.Decrement(ref inFlightCount));
    }
}
