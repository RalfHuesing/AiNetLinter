#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.ExternalSource.Repository;

internal sealed record CheckoutValidationResult(
    string? SolutionPath,
    ExternalSourceProviderFailureKind FailureKind,
    ImmutableArray<ExternalSourceConfigurationDiagnostic> Diagnostics)
{
    internal bool IsValid => SolutionPath is not null;

    internal static CheckoutValidationResult Success(string solutionPath) =>
        new(
            solutionPath,
            ExternalSourceProviderFailureKind.None,
            ImmutableArray<ExternalSourceConfigurationDiagnostic>.Empty);

    internal static CheckoutValidationResult Failure(
        ExternalSourceProviderFailureKind failureKind,
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        new(
            null,
            failureKind,
            diagnostics.ToImmutableArray());
}
