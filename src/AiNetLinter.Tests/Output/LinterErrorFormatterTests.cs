#nullable enable

using Xunit;
using AiNetLinter.Output;

namespace AiNetLinter.Tests.Output;

/// <summary>
/// Tests fuer <see cref="LinterErrorFormatter"/> und <see cref="LinterErrorCodes"/>.
/// </summary>
public sealed class LinterErrorFormatterTests
{
    [Fact]
    public void Format_WithCodeAndMessage_ProducesExpectedPrefix()
    {
        var result = LinterErrorFormatter.Format(LinterErrorCodes.ConfigNotFound, "Datei nicht gefunden.");

        Assert.StartsWith("[ERROR]: CONFIG_NOT_FOUND:", result);
        Assert.Contains("Datei nicht gefunden.", result);
    }

    [Fact]
    public void Format_WithContext_IncludesContextLine()
    {
        var result = LinterErrorFormatter.Format(
            LinterErrorCodes.ConfigInvalid,
            "Fehler.",
            context: "rules.json");

        Assert.Contains("context: rules.json", result);
    }

    [Fact]
    public void Format_WithHint_IncludesHintLine()
    {
        var result = LinterErrorFormatter.Format(
            LinterErrorCodes.BaselineNotFound,
            "Baseline fehlt.",
            hint: "Mit --create-baseline neu erzeugen.");

        Assert.Contains("hint:", result);
        Assert.Contains("--create-baseline", result);
    }

    [Fact]
    public void Format_WithoutContextOrHint_ProducesSingleLine()
    {
        var result = LinterErrorFormatter.Format(LinterErrorCodes.AnalysisFailed, "Kurzfehler.");

        Assert.DoesNotContain("\n", result);
    }

    [Fact]
    public void Format_ThenWriteError_WritesStructuredMessage()
    {
        var console = new TestLintConsole();
        console.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.WorkspaceDiagnostic,
            "Workspace-Problem.", context: "foo.slnx"));

        Assert.Single(console.Errors);
        Assert.Contains("WORKSPACE_DIAGNOSTIC", console.Errors[0]);
        Assert.Contains("foo.slnx", console.Errors[0]);
    }

    [Fact]
    public void AllErrorCodes_AreNonEmpty()
    {
        Assert.NotEmpty(LinterErrorCodes.ConfigRequired);
        Assert.NotEmpty(LinterErrorCodes.ConfigNotFound);
        Assert.NotEmpty(LinterErrorCodes.ConfigInvalid);
        Assert.NotEmpty(LinterErrorCodes.ConfigSmell);
        Assert.NotEmpty(LinterErrorCodes.BaselineNotFound);
        Assert.NotEmpty(LinterErrorCodes.BaselineInvalid);
        Assert.NotEmpty(LinterErrorCodes.WorkspaceDiagnostic);
        Assert.NotEmpty(LinterErrorCodes.AnalysisFailed);
        Assert.NotEmpty(LinterErrorCodes.ResourceNotFound);
        Assert.NotEmpty(LinterErrorCodes.DriftDetected);
    }
}
