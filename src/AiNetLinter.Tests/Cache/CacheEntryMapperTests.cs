#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;
using AiNetLinter.Cache;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Tests.Cache;

public sealed class CacheEntryMapperTests
{
    [Fact]
    public void Mapper_MapsViolationsCorrectly()
    {
        var dto = new RuleViolationDto("file.cs", 10, "RuleName", "Details", "Guidance");
        var violation = CacheEntryMapper.ToViolation(dto);

        Assert.Equal(dto.FilePath, violation.FilePath);
        Assert.Equal(dto.LineNumber, violation.LineNumber);
        Assert.Equal(dto.RuleName, violation.RuleName);
        Assert.Equal(dto.Details, violation.Details);
        Assert.Equal(dto.Guidance, violation.Guidance);
    }

    [Fact]
    public void Mapper_MapsClassInfoCorrectly()
    {
        var dto = new ClassInfoDto(
            "ClassName",
            "file.cs",
            5,
            3,
            2,
            1200,
            new[] { new FootprintDetailDto("Dep", 400) },
            true,
            false,
            true,
            new[] { "BaseType" },
            "ProjName"
        );

        var classInfo = CacheEntryMapper.ToClassInfo(dto);

        Assert.Equal(dto.Name, classInfo.Name);
        Assert.Equal(dto.FilePath, classInfo.FilePath);
        Assert.Equal(dto.LineNumber, classInfo.LineNumber);
        Assert.Equal(dto.MaxCognitiveComplexity, classInfo.MaxCognitiveComplexity);
        Assert.Equal(dto.InheritanceDepth, classInfo.InheritanceDepth);
        Assert.Equal(dto.AiContextFootprint, classInfo.AIContextFootprint);
        Assert.Single(classInfo.AIContextFootprintDetails);
        Assert.Equal("Dep", classInfo.AIContextFootprintDetails[0].Name);
        Assert.Equal(400, classInfo.AIContextFootprintDetails[0].Lines);
        Assert.True(classInfo.HasTestMethods);
        Assert.False(classInfo.IsPartial);
        Assert.True(classInfo.IsStatic);
        Assert.Equal("BaseType", classInfo.BaseTypeNames.First());
        Assert.Equal("ProjName", classInfo.ProjectName);
    }

    [Fact]
    public void Mapper_MapsPartialPartCorrectly()
    {
        var dto = new PartialPartDto("TypeName", "file.cs", 1, 200);
        var part = CacheEntryMapper.ToPartialPart(dto);

        Assert.Equal(dto.TypeName, part.TypeName);
        Assert.Equal(dto.FilePath, part.FilePath);
        Assert.Equal(dto.LineNumber, part.LineNumber);
        Assert.Equal(dto.FileLineCount, part.FileLineCount);
    }

    [Fact]
    public void Mapper_RestoreToState_RestoresSourceFileAndTestFileCorrectly()
    {
        var state = new AnalysisState(
            null!, // Solution not needed for restoring state
            new ConcurrentBag<RuleViolation>(),
            new TestCoverageIndex(),
            new ConcurrentBag<ClassInfo>(),
            new ConcurrentBag<PartialClassPart>(),
            new ConcurrentDictionary<string, string>(),
            new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default)
        );

        var entry = new AnalysisCacheEntry
        {
            RelativePath = "src/Test.cs",
            Checksum = "hash",
            Violations = new[]
            {
                new RuleViolationDto("src/Test.cs", 10, "Rule1", "Details1", "Guidance1")
            },
            Classes = new[]
            {
                new ClassInfoDto("Class1", "src/Test.cs", 5, 0, 0, 0, Array.Empty<FootprintDetailDto>(), false, false, false, Array.Empty<string>(), null)
            },
            PartialParts = new[]
            {
                new PartialPartDto("Class1", "src/Test.cs", 1, 100)
            },
            TestSignals = new TestSignalsDto
            {
                TestClassNames = new[] { "TestClass" },
                ReferencedTypeNames = new[] { "TargetClass" },
                CoversComments = new[] { "CoveredType" }
            }
        };

        // 1. Restore as a source file (isTestFile: false)
        CacheEntryMapper.RestoreToState(entry, state, isTestFile: false);

        Assert.Single(state.Violations);
        Assert.Single(state.SourceClasses);
        Assert.Single(state.PartialClassParts);
        Assert.Empty(state.TestCoverage.TestClassNames); // Should not restore test signals for non-test files

        // 2. Clear state and restore as a test file (isTestFile: true)
        var state2 = new AnalysisState(
            null!,
            new ConcurrentBag<RuleViolation>(),
            new TestCoverageIndex(),
            new ConcurrentBag<ClassInfo>(),
            new ConcurrentBag<PartialClassPart>(),
            new ConcurrentDictionary<string, string>(),
            new ConcurrentDictionary<INamedTypeSymbol, FieldReadonlyTracker>(SymbolEqualityComparer.Default)
        );

        CacheEntryMapper.RestoreToState(entry, state2, isTestFile: true);

        Assert.Single(state2.Violations);
        Assert.Empty(state2.SourceClasses); // Should not restore classes for test files
        Assert.Empty(state2.PartialClassParts); // Should not restore partial parts for test files
        Assert.Single(state2.TestCoverage.TestClassNames);
        Assert.Equal("TestClass", state2.TestCoverage.TestClassNames.First());
        Assert.Single(state2.TestCoverage.ReferencedTypeNames);
        Assert.Equal("TargetClass", state2.TestCoverage.ReferencedTypeNames.First());
        Assert.Single(state2.TestCoverage.CoversComments);
        Assert.Equal("CoveredType", state2.TestCoverage.CoversComments.First());
    }
}
