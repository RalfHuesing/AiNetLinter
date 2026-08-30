#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Configuration;

namespace AiNetLinter.Mcp.Assemblies.Analysis;

internal static class AssemblyAnalysisDiagnostics
{
    internal static IReadOnlyList<string> FormatExternalDiagnostics(
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => !string.IsNullOrWhiteSpace(diagnostic.Message))
            .Select(diagnostic =>
                $"External-Source-Diagnose [{diagnostic.Severity}] {diagnostic.Code}: {diagnostic.Message} ({diagnostic.Location})")
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .ToList();

    internal static string GetConfigurationFailureCode(
        IEnumerable<ExternalSourceConfigurationDiagnostic> diagnostics) =>
        diagnostics.FirstOrDefault()?.Code
            ?? ExternalSourceConfigurationDiagnosticCodes.ExternalSourcesSectionInvalid;
}
