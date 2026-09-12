#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiNetLinter.Mcp.Wire;

/// <summary>
/// Rendert die einzige agentisch sichtbare Textdarstellung aus vollständigen Evidenzeinheiten.
/// </summary>
internal sealed class AgentContentRenderer
{
    internal AgentContentRenderResult Render(AgentContentRenderRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var required = request.Evidence.Where(evidence => evidence.IsRequired).ToList();
        var minimumResponseBytes = GetUtf8ByteCount(required);
        if (request.MaxResponseBytes > 0 && minimumResponseBytes > request.MaxResponseBytes)
        {
            return AgentContentRenderResult.BudgetTooSmall(minimumResponseBytes);
        }

        var selected = new List<AgentContentEvidence>(required);
        foreach (var evidence in request.Evidence.Where(evidence => !evidence.IsRequired))
        {
            var candidate = selected.Append(evidence).ToList();
            if (request.MaxResponseBytes > 0 && GetUtf8ByteCount(candidate) > request.MaxResponseBytes) continue;
            selected.Add(evidence);
        }

        var text = RenderEvidence(selected);
        var textBytes = Encoding.UTF8.GetByteCount(text);
        if (request.MaxResponseBytes > 0 && textBytes > request.MaxResponseBytes)
        {
            return AgentContentRenderResult.BudgetTooSmall(textBytes);
        }

        return new AgentContentRenderResult(
            text,
            IsTruncated: selected.Count != request.Evidence.Count,
            IsBudgetTooSmall: false,
            MinimumResponseBytes: null);
    }

    private static int GetUtf8ByteCount(IReadOnlyList<AgentContentEvidence> evidence) =>
        Encoding.UTF8.GetByteCount(RenderEvidence(evidence));

    private static string RenderEvidence(IEnumerable<AgentContentEvidence> evidence) =>
        string.Join("\n", evidence.Select(item => item.Text));
}

internal sealed record AgentContentRenderRequest(
    bool IsError,
    IReadOnlyList<AgentContentEvidence> Evidence,
    int MaxResponseBytes = 0);

internal sealed record AgentContentEvidence(string Text, bool IsRequired);

internal sealed record AgentContentRenderResult(
    string Text,
    bool IsTruncated,
    bool IsBudgetTooSmall,
    int? MinimumResponseBytes)
{
    internal static AgentContentRenderResult BudgetTooSmall(int minimumResponseBytes) =>
        new(string.Empty, IsTruncated: false, IsBudgetTooSmall: true, minimumResponseBytes);
}
