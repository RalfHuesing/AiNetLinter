#nullable enable

using System;
using System.IO;
using AiNetLinter.Evals;
using Xunit;

namespace AiNetLinter.Tests.Evals;

public sealed class EvalAssemblerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly EvalDefinition _namingDriftEval;
    private readonly EvalDefinition _architectureEval;

    public EvalAssemblerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiNetLinter_EvalAssemblerTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        // Erstelle eine Dummy-.cs-Datei, damit der Vocabulary/Structure Map Builder etwas findet
        var dummyFile = Path.Combine(_tempDir, "FooChecker.cs");
        File.WriteAllText(dummyFile, "public class FooChecker {}");

        _namingDriftEval = EvalRegistry.TryResolve("naming-drift") 
            ?? throw new InvalidOperationException("naming-drift not found");
        _architectureEval = EvalRegistry.TryResolve("architecture-intent") 
            ?? throw new InvalidOperationException("architecture-intent not found");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Assemble_ReplacesGeneratedAtPlaceholder()
    {
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, "spec", "2026-01-01 12:00");
        Assert.DoesNotContain("{{GENERATED_AT}}", result);
        Assert.Contains("2026-01-01 12:00", result);
    }

    [Fact]
    public void Assemble_NamingDrift_ContainsVocabularyMapOutput()
    {
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, "spec", "2026-01-01");
        Assert.Contains("FooChecker", result);
    }

    [Fact]
    public void Assemble_ArchitectureIntent_ContainsStructureMapOutput()
    {
        var result = EvalAssembler.Assemble(_architectureEval, _tempDir, "spec", "2026-01-01");
        Assert.Contains("FooChecker.cs", result);
    }

    [Fact]
    public void Assemble_NamingDrift_DoesNotContainStructureMapPlaceholder()
    {
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, "spec", "2026-01-01");
        Assert.DoesNotContain("{{STRUCTURE_MAP}}", result);
    }

    [Fact]
    public void Assemble_SpecInlinedInOutput()
    {
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, "MY_UNIQUE_SPEC", "2026-01-01");
        Assert.Contains("MY_UNIQUE_SPEC", result);
    }
}
