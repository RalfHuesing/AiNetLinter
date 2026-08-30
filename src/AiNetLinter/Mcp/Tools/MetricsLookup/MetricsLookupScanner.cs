#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.Metrics;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.MetricsLookup;

/// <summary>
/// Scannt ein aufgelöstes Roslyn-Symbol und berechnet präzise Metriken sowie Schwellwert-Abgleiche.
/// </summary>
internal static class MetricsLookupScanner
{
    internal static MetricsLookupResultDto ScanSymbol(
        ISymbol symbol,
        ILinterEngineConfig config,
        string solutionRoot,
        CancellationToken ct,
        AnalysisSymbolIdentity? assemblyIdentity = null)
    {
        var symbolName = symbol.Name;
        var symbolKind = symbol.Kind.ToString();
        var qualifiedName = symbol.ToDisplayString();
        var docCommentId = assemblyIdentity?.Format(
            symbol.TryGetDocCommentId() ?? CallGraphTraversal.GetStableSymbolId(symbol))
            ?? symbol.TryGetDocCommentId();
        var location = ExtractLocation(symbol, solutionRoot);

        MethodMetricsDto? methodMetrics = null;
        TypeMetricsDto? typeMetrics = null;
        PropertyMetricsDto? propertyMetrics = null;
        List<ThresholdCheckDto> thresholdChecks;

        if (symbol is IMethodSymbol methodSymbol)
        {
            (methodMetrics, thresholdChecks) = ScanMethod(methodSymbol, config, ct);
        }
        else if (symbol is INamedTypeSymbol typeSymbol)
        {
            (typeMetrics, thresholdChecks) = ScanType(typeSymbol, config, ct);
        }
        else if (symbol is IPropertySymbol propertySymbol)
        {
            (propertyMetrics, thresholdChecks) = ScanProperty(propertySymbol, config, ct);
        }
        else
        {
            thresholdChecks = ScanFallback(symbol, ct);
        }

        return new MetricsLookupResultDto(
            SymbolName: symbolName,
            SymbolKind: symbolKind,
            QualifiedName: qualifiedName,
            DocCommentId: docCommentId,
            Location: location,
            MethodMetrics: methodMetrics,
            TypeMetrics: typeMetrics,
            PropertyMetrics: propertyMetrics,
            ThresholdChecks: thresholdChecks
        );
    }

    private static (MethodMetricsDto, List<ThresholdCheckDto>) ScanMethod(
        IMethodSymbol method, ILinterEngineConfig config, CancellationToken ct)
    {
        var syntax = method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct);
        var codeLines = syntax != null ? MethodLineCounter.GetCodeLineCount(syntax) : 0;
        var cc = syntax != null ? ComplexityCalculator.GetCyclomaticComplexity(syntax) : 1;
        var cogC = syntax != null ? ComplexityCalculator.GetCognitiveComplexity(syntax) : 0;

        var (effectiveParams, ignoredParams) = CalculateParameters(method, config);

        var metrics = new MethodMetricsDto(
            CodeLines: codeLines,
            CyclomaticComplexity: cc,
            CognitiveComplexity: cogC,
            TotalParameters: method.Parameters.Length,
            EffectiveParameters: effectiveParams,
            IgnoredParameters: ignoredParams
        );

        var lineLimit = config.Metrics.MaxMethodLineCount;
        var methodMetricsDict = new Dictionary<string, int>
        {
            [MetricNames.LineCount] = codeLines,
            [MetricNames.CyclomaticComplexity] = cc,
            [MetricNames.CognitiveComplexity] = cogC
        };
        var lineSuppression = CompoundSuppressionEvaluator.Evaluate(
            LinterRuleIds.MaxMethodLineCount, config.Metrics.CompoundSuppressions, methodMetricsDict);
        if (lineSuppression > 0)
        {
            lineLimit = lineSuppression;
        }
        else if (lineSuppression == 0)
        {
            lineLimit = 0;
        }

        var checks = new List<ThresholdCheckDto>
        {
            CheckThreshold(
                MetricNames.LineCount, codeLines, lineLimit,
                LinterRuleIds.MaxMethodLineCount, config.Metrics.ComplexityNearMissTolerance),
            CheckThreshold(
                MetricNames.CyclomaticComplexity, cc, config.Metrics.MaxCyclomaticComplexity,
                LinterRuleIds.MaxCyclomaticComplexity, config.Metrics.ComplexityNearMissTolerance),
            CheckThreshold(
                MetricNames.CognitiveComplexity, cogC, config.Metrics.MaxCognitiveComplexity,
                LinterRuleIds.MaxCognitiveComplexity, config.Metrics.ComplexityNearMissTolerance),
            CheckThreshold(
                MetricNames.ParameterCount, effectiveParams, config.Metrics.MaxMethodParameterCount,
                LinterRuleIds.MaxMethodParameterCount, 0),
        };

        return (metrics, checks);
    }

    private static (TypeMetricsDto, List<ThresholdCheckDto>) ScanType(
        INamedTypeSymbol type, ILinterEngineConfig config, CancellationToken ct)
    {
        var codeLines = type.DeclaringSyntaxReferences
            .Sum(r => MethodLineCounter.GetCodeLineCount(r.GetSyntax(ct)));

        var (footprint, topDeps) = AIContextFootprintCalculator.CalculateDetailed(
            type,
            config.Metrics.FootprintIgnoreNamespacePrefixes,
            config.Metrics.FootprintIgnoreTypeNames);

        var members = type.GetMembers().Where(IsCountableMember).ToList();
        var publicMembers = members.Count(IsPublicMember);
        var methodCount = members.OfType<IMethodSymbol>().Count();
        var propertyCount = members.OfType<IPropertySymbol>().Count();

        var topDepsDto = topDeps.Select(d => new TopDependencyDto(d.Name, d.Lines)).ToList();

        var metrics = new TypeMetricsDto(
            CodeLines: codeLines,
            AiContextFootprint: footprint,
            PublicMemberCount: publicMembers,
            TotalMemberCount: members.Count,
            MethodCount: methodCount,
            PropertyCount: propertyCount,
            TopDependencies: topDepsDto
        );

        var isPublicMembersExempt = config.Metrics.MaxPublicMembersPerTypeExemptSuffixes?.Any(
            s => type.Name.EndsWith(s, StringComparison.OrdinalIgnoreCase)) == true;

        var checks = new List<ThresholdCheckDto>
        {
            CheckThreshold(
                MetricNames.LineCount, codeLines, config.Metrics.MaxLineCount,
                LinterRuleIds.MaxLineCount, 0),
            CheckThreshold(
                LinterRuleIds.AIContextFootprint, footprint, config.Metrics.MaxAIContextFootprint,
                LinterRuleIds.AIContextFootprint, 0),
        };

        if (config.Metrics.MaxPublicMembersPerType > 0)
        {
            checks.Add(CheckThreshold(
                MetricNames.PublicMemberCount, publicMembers, config.Metrics.MaxPublicMembersPerType,
                LinterRuleIds.MaxPublicMembersPerType, 0, isExempt: isPublicMembersExempt));
        }

        return (metrics, checks);
    }

    private static (PropertyMetricsDto, List<ThresholdCheckDto>) ScanProperty(
        IPropertySymbol prop, ILinterEngineConfig config, CancellationToken ct)
    {
        var syntax = prop.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct);
        var codeLines = syntax != null ? MethodLineCounter.GetCodeLineCount(syntax) : 0;
        var cc = syntax != null ? ComplexityCalculator.GetCyclomaticComplexity(syntax) : 1;
        var cogC = syntax != null ? ComplexityCalculator.GetCognitiveComplexity(syntax) : 0;

        var metrics = new PropertyMetricsDto(
            CodeLines: codeLines,
            CyclomaticComplexity: cc,
            CognitiveComplexity: cogC,
            HasGetter: prop.GetMethod != null,
            HasSetter: prop.SetMethod != null
        );

        var checks = new List<ThresholdCheckDto>
        {
            CheckThreshold(
                MetricNames.CyclomaticComplexity, cc, config.Metrics.MaxCyclomaticComplexity,
                LinterRuleIds.MaxCyclomaticComplexity, config.Metrics.ComplexityNearMissTolerance),
            CheckThreshold(
                MetricNames.CognitiveComplexity, cogC, config.Metrics.MaxCognitiveComplexity,
                LinterRuleIds.MaxCognitiveComplexity, config.Metrics.ComplexityNearMissTolerance),
        };

        return (metrics, checks);
    }

    private static List<ThresholdCheckDto> ScanFallback(ISymbol symbol, CancellationToken ct)
    {
        var checks = new List<ThresholdCheckDto>();
        var syntax = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(ct);
        if (syntax != null)
        {
            var codeLines = MethodLineCounter.GetCodeLineCount(syntax);
            checks.Add(new ThresholdCheckDto(
                Metric: MetricNames.LineCount,
                Value: codeLines,
                Limit: 0,
                Status: ThresholdStatus.Ok,
                RuleId: "-"
            ));
        }

        return checks;
    }

    private static (int EffectiveCount, List<string> Ignored) CalculateParameters(
        IMethodSymbol method, ILinterEngineConfig config)
    {
        var ignoreNames = config.Metrics.MethodParameterCountIgnoreTypeNames;
        var ignorePrefixes = config.Metrics.MethodParameterCountIgnoreTypePrefixes;
        var ignored = new List<string>();
        var effective = 0;

        foreach (var p in method.Parameters)
        {
            var typeName = p.Type.Name;
            var isIgnored = (ignoreNames != null && ignoreNames.Contains(typeName, StringComparer.Ordinal))
                         || (ignorePrefixes != null && ignorePrefixes.Any(pfx => typeName.StartsWith(pfx, StringComparison.OrdinalIgnoreCase)));

            if (isIgnored)
            {
                ignored.Add($"{p.Name} ({typeName})");
            }
            else
            {
                effective++;
            }
        }

        return (effective, ignored);
    }

    private static ThresholdCheckDto CheckThreshold(
        string metric, int value, int limit, string ruleId, int nearMissTolerance, bool isExempt = false)
    {
        string status;
        if (limit <= 0 || isExempt)
        {
            status = ThresholdStatus.Ok;
        }
        else if (value > limit)
        {
            status = ThresholdStatus.Violation;
        }
        else if (nearMissTolerance > 0 && value >= limit - nearMissTolerance)
        {
            status = ThresholdStatus.Warn;
        }
        else
        {
            status = ThresholdStatus.Ok;
        }

        return new ThresholdCheckDto(metric, value, limit, status, ruleId);
    }

    private static bool IsCountableMember(ISymbol member)
    {
        if (member.IsImplicitlyDeclared) return false;
        if (member is IMethodSymbol method)
        {
            return method.MethodKind == MethodKind.Ordinary || method.MethodKind == MethodKind.Constructor;
        }
        return member is IPropertySymbol or IFieldSymbol or IEventSymbol;
    }

    private static bool IsPublicMember(ISymbol member)
    {
        if (!IsCountableMember(member)) return false;
        if (member.DeclaredAccessibility != Accessibility.Public) return false;
        if (member.IsOverride) return false;
        if (member is IMethodSymbol method && method.ExplicitInterfaceImplementations.Length > 0) return false;
        if (member is IPropertySymbol prop && prop.ExplicitInterfaceImplementations.Length > 0) return false;
        if (member is IEventSymbol evt && evt.ExplicitInterfaceImplementations.Length > 0) return false;
        return true;
    }

    private static SymbolLocationDto? ExtractLocation(ISymbol symbol, string solutionRoot)
    {
        var loc = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (loc?.SourceTree == null) return null;

        var lineSpan = loc.GetLineSpan();
        var relPath = PathNormalizer.ToRelative(solutionRoot, loc.SourceTree.FilePath);
        return new SymbolLocationDto(
            FilePath: relPath,
            StartLine: lineSpan.StartLinePosition.Line + 1,
            EndLine: lineSpan.EndLinePosition.Line + 1
        );
    }

}
