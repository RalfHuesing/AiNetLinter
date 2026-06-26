#nullable enable

using System;
using System.IO;
using AiNetLinter.Evals;
using Xunit;

namespace AiNetLinter.Tests.Evals;

[Collection("ConsoleTestCollection")]
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

    [Fact]
    public void Assemble_SpecWrappedInSpecsXmlTag()
    {
        const string specContent = "UNIQUE_SPEC_MARKER";
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, specContent, "2026-01-01");
        var specsOpen  = result.IndexOf("<specs>",  StringComparison.Ordinal);
        var specsClose = result.IndexOf("</specs>", StringComparison.Ordinal);
        var markerPos  = result.IndexOf(specContent, StringComparison.Ordinal);
        Assert.True(specsOpen > -1,   "<specs> tag muss im Prompt vorhanden sein.");
        Assert.True(specsOpen < markerPos && markerPos < specsClose,
            "Spec-Inhalt muss zwischen <specs> und </specs> stehen.");
    }

    [Fact]
    public void Assemble_NamingDrift_TaskInstructionBeforeSpec()
    {
        const string specContent = "MY_SPEC_CONTENT";
        var result = EvalAssembler.Assemble(_namingDriftEval, _tempDir, specContent, "2026-01-01");
        var taskPos = result.IndexOf("Deine Aufgabe", StringComparison.Ordinal);
        var specPos = result.IndexOf(specContent,     StringComparison.Ordinal);
        Assert.True(taskPos > -1, "Template muss 'Deine Aufgabe' enthalten.");
        Assert.True(taskPos < specPos, "Task-Instruktion muss vor dem Spec-Inhalt erscheinen.");
    }

    [Fact]
    public void Assemble_BothTemplates_ContainRecommendationsSection()
    {
        var namingResult = EvalAssembler.Assemble(_namingDriftEval,     _tempDir, "spec", "2026-01-01");
        var archResult   = EvalAssembler.Assemble(_architectureEval, _tempDir, "spec", "2026-01-01");
        Assert.Contains("Empfehlungen",    namingResult);
        Assert.Contains("P1",              namingResult);
        Assert.Contains("Empfehlungen",    archResult);
        Assert.Contains("P1",              archResult);
    }

    [Fact]
    public void Assemble_LargePrompt_WritesWarningToStdErr()
    {
        var originalError = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            // 65 000 Zeichen / 4 ≈ 16 250 Token → überschreitet Schwelle von 15 000
            var largeSpec = new string('x', 65_000);
            EvalAssembler.Assemble(_namingDriftEval, _tempDir, largeSpec, "2026-01-01");
            Assert.Contains("[WARN]", capture.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Assemble_SmallPrompt_NoWarningOnStdErr()
    {
        var originalError = Console.Error;
        var capture = new StringWriter();
        Console.SetError(capture);
        try
        {
            EvalAssembler.Assemble(_namingDriftEval, _tempDir, "kurze spec", "2026-01-01");
            Assert.Empty(capture.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Assemble_ArchitectureIntent_TaskInstructionBeforeSpec()
    {
        const string specContent = "MY_ARCH_SPEC_CONTENT";
        var result = EvalAssembler.Assemble(_architectureEval, _tempDir, specContent, "2026-01-01");
        var taskPos = result.IndexOf("Deine Aufgabe", StringComparison.Ordinal);
        var specPos = result.IndexOf(specContent,     StringComparison.Ordinal);
        Assert.True(taskPos > -1, "Template muss 'Deine Aufgabe' enthalten.");
        Assert.True(taskPos < specPos, "Task-Instruktion muss vor dem Spec-Inhalt erscheinen.");
    }
}
