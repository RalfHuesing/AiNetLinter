#nullable enable

namespace AiNetLinter.Mcp.Assemblies;

internal enum ExternalSourceRepositoryHealth
{
    Verified,
    Degraded,
    Unavailable,
}

internal enum ExternalSourceCheckoutTrust
{
    Clean,
    Dirty,
    Unverified,
}

internal sealed record ExternalSourceRepositoryResultState
{
    internal ExternalSourceProviderFailureKind FailureKind { get; init; }

    internal ExternalSourceRepositoryHealth? Health { get; init; }

    internal string? LastGoodRevision { get; init; }

    internal ExternalSourceCheckoutTrust? CheckoutTrust { get; init; }

    internal static ExternalSourceRepositoryResultState Create(
        ExternalSourceProviderFailureKind failureKind = ExternalSourceProviderFailureKind.None,
        ExternalSourceRepositoryHealth? health = null,
        string? lastGoodRevision = null,
        ExternalSourceCheckoutTrust? checkoutTrust = null) =>
        new()
        {
            FailureKind = failureKind,
            Health = health,
            LastGoodRevision = lastGoodRevision,
            CheckoutTrust = checkoutTrust,
        };
}
