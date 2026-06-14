#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Cache;

internal static class CacheEntryMapper
{
    public static RuleViolation ToViolation(RuleViolationDto dto) => new()
    {
        FilePath = dto.FilePath,
        LineNumber = dto.LineNumber,
        RuleName = dto.RuleName,
        Details = dto.Details,
        Guidance = dto.Guidance,
    };

    public static ClassInfo ToClassInfo(ClassInfoDto dto) => new()
    {
        Name = dto.Name,
        FilePath = dto.FilePath,
        LineNumber = dto.LineNumber,
        MaxCognitiveComplexity = dto.MaxCognitiveComplexity,
        InheritanceDepth = dto.InheritanceDepth,
        AIContextFootprint = dto.AiContextFootprint,
        AIContextFootprintDetails = dto.AiContextFootprintDetails
            .Select(d => (d.Name, d.Lines)).ToArray(),
        HasTestMethods = dto.HasTestMethods,
        IsPartial = dto.IsPartial,
        IsStatic = dto.IsStatic,
        BaseTypeNames = dto.BaseTypeNames.ToArray(),
        ProjectName = dto.ProjectName,
    };

    public static PartialClassPart ToPartialPart(PartialPartDto dto) =>
        new(dto.TypeName, dto.FilePath, dto.LineNumber, dto.FileLineCount);

    public static void RestoreToState(AnalysisCacheEntry entry, AnalysisState state, bool isTestFile)
    {
        foreach (var v in entry.Violations)
        {
            state.Violations.Add(ToViolation(v));
        }

        if (isTestFile)
        {
            var signals = entry.TestSignals;
            foreach (var n in signals.TestClassNames)
            {
                state.TestCoverage.AddTestClass(n);
            }
            foreach (var n in signals.ReferencedTypeNames)
            {
                state.TestCoverage.AddReferencedType(n);
            }
            foreach (var n in signals.CoversComments)
            {
                state.TestCoverage.AddCoversComment(n);
            }
        }
        else
        {
            foreach (var c in entry.Classes)
            {
                var cls = ToClassInfo(c);
                state.SourceClasses.Add(cls);
            }

            foreach (var p in entry.PartialParts)
            {
                state.PartialClassParts.Add(ToPartialPart(p));
            }
        }
    }

    public static AnalysisCacheEntry BuildEntry(
        string relativePath,
        string checksum,
        LinterAnalyzer analyzer,
        IEnumerable<PartialClassPart> partialParts,
        TestSignalsDto testSignals)
    {
        return new AnalysisCacheEntry
        {
            RelativePath = relativePath,
            Checksum = checksum,
            Violations = analyzer.Violations.Select(v => new RuleViolationDto(
                v.FilePath, v.LineNumber, v.RuleName, v.Details, v.Guidance)).ToArray(),
            Classes = analyzer.Classes.Select(c => new ClassInfoDto(
                c.Name, c.FilePath, c.LineNumber,
                c.MaxCognitiveComplexity, c.InheritanceDepth, c.AIContextFootprint,
                c.AIContextFootprintDetails.Select(d => new FootprintDetailDto(d.Name, d.Lines)).ToArray(),
                c.HasTestMethods, c.IsPartial, c.IsStatic,
                c.BaseTypeNames.ToArray(), c.ProjectName)).ToArray(),
            PartialParts = partialParts.Select(p =>
                new PartialPartDto(p.TypeName, p.FilePath, p.LineNumber, p.FileLineCount)).ToArray(),
            TestSignals = testSignals,
        };
    }
}
