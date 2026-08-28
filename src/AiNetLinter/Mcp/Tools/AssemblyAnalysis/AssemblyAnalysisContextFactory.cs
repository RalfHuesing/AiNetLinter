#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal static class AssemblyAnalysisContextFactory
{
    internal static async Task<(AssemblyContext? Context, string? Error)> CreateAsync(
        string assemblyPath,
        Solution? consumerSolution,
        string? receiverType,
        CancellationToken cancellationToken)
    {
        await using var session = new AssemblyAnalysisSession(assemblyPath);
        var refresh = await session.RefreshAsync(cancellationToken).ConfigureAwait(false);
        var generation = session.CurrentGeneration;
        if (generation is null)
        {
            return (null, FormatFailure(refresh.Diagnostics));
        }

        var diagnostics = generation.Diagnostics.Select(diagnostic => diagnostic.Message).ToList();
        var consumer = consumerSolution is null
            ? new ConsumerSelection(null, null)
            : await FindConsumerReceiverAsync(consumerSolution, receiverType, diagnostics, cancellationToken).ConfigureAwait(false);
        return (new AssemblyContext(
            generation.Snapshot.Compilation.Assembly,
            generation.Identity,
            generation.References,
            DistinctDiagnostics(diagnostics),
            generation.Snapshot.Compilation,
            consumer.Receiver,
            consumer.ProjectName,
            generation.Origin,
            generation.Number,
            generation.Status), null);
    }

    private static async Task<ConsumerSelection> FindConsumerReceiverAsync(
        Solution solution,
        string? receiverType,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var project in solution.Projects.OrderBy(project => project.Name, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Compilation? compilation;
            try
            {
                compilation = await project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException)
            {
                diagnostics.Add($"Consumer-Compilation '{project.Name}' konnte nicht geladen werden: {ex.Message}");
                continue;
            }

            if (compilation is null) continue;
            var receiver = ResolveReceiver(compilation, receiverType);
            if (receiver is not null) return new ConsumerSelection(receiver, project.Name);
        }

        if (!string.IsNullOrWhiteSpace(receiverType))
        {
            diagnostics.Add($"Consumer-Typ '{receiverType}' konnte in keiner geladenen Compilation aufgelöst werden.");
        }

        return new ConsumerSelection(null, null);
    }

    private static ITypeSymbol? ResolveReceiver(Compilation compilation, string? receiverType)
    {
        if (string.IsNullOrWhiteSpace(receiverType)) return null;
        var normalized = receiverType.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
        return compilation.GetTypeByMetadataName(normalized)
            ?? AssemblyAnalysisSymbolTraversal.GetAllTypes(compilation.GlobalNamespace)
                .FirstOrDefault(type => string.Equals(
                    type.ToDisplayString(),
                    normalized,
                    StringComparison.Ordinal));
    }

    private static string FormatFailure(IReadOnlyList<AssemblySessionDiagnostic> diagnostics) =>
        diagnostics.Count == 0
            ? "Assembly konnte nicht analysiert werden."
            : string.Join(" ", diagnostics.Select(diagnostic => diagnostic.Message));

    private static IReadOnlyList<string> DistinctDiagnostics(IEnumerable<string> diagnostics) =>
        diagnostics.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).Take(100).ToList();

    private sealed record ConsumerSelection(ITypeSymbol? Receiver, string? ProjectName);
}
