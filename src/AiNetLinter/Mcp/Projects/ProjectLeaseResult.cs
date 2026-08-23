#nullable enable

namespace AiNetLinter.Mcp.Projects;

internal sealed record ProjectLeaseResult
{
    internal bool Succeeded => Lease is not null;

    public ProjectLease? Lease { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    internal static ProjectLeaseResult Success(ProjectLease lease) => new() { Lease = lease };

    internal static ProjectLeaseResult Failure(string errorCode, string errorMessage) =>
        new() { ErrorCode = errorCode, ErrorMessage = errorMessage };
}
