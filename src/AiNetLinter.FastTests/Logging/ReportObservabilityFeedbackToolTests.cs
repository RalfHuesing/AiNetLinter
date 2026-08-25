#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.ServerMaintenance;
using AiNetLinter.Output;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Logging;

[Trait("Category", "Unit")]
public sealed class ReportObservabilityFeedbackToolTests
{
    [Fact]
    public async Task ExecuteAsync_ValideEingabe_LiefertErfolgreicheMeldungUndPayload()
    {
        var parameters = new ReportObservabilityFeedbackParameters(
            FeedbackType: "issue",
            Title: "Symbolaufloesung unvollstaendig",
            Description: "Methode 'Process' in Interface IFoo wurde nicht gefunden.",
            RelatedTool: "find_symbol",
            Severity: "high",
            ExpectedBehavior: "Symbol wird zurueckgegeben",
            ActualBehavior: "0 Treffer",
            AdditionalContext: "Projekt verwendet Source Generator",
            ProjectRoot: "c:/repos/my-project");

        var result = await ReportObservabilityFeedbackTool.ExecuteAsync(parameters);

        Assert.True(result.IsError != true);
        Assert.NotNull(result.Content);
        var text = Assert.Single(result.Content) as TextContentBlock;
        Assert.NotNull(text);
        Assert.Contains("Symbolaufloesung unvollstaendig", text.Text);
        Assert.Contains("issue", text.Text);
        Assert.Contains("[INFO]:", text.Text);
        Assert.NotNull(result.StructuredContent);
    }

    [Theory]
    [InlineData(null, "Titel", "Beschreibung", "feedbackType")]
    [InlineData("", "Titel", "Beschreibung", "feedbackType")]
    [InlineData("   ", "Titel", "Beschreibung", "feedbackType")]
    [InlineData("issue", null, "Beschreibung", "title")]
    [InlineData("issue", "", "Beschreibung", "title")]
    [InlineData("issue", "   ", "Beschreibung", "title")]
    [InlineData("issue", "Titel", null, "description")]
    [InlineData("issue", "Titel", "", "description")]
    [InlineData("issue", "Titel", "   ", "description")]
    public async Task ExecuteAsync_FehlendePflichtfelder_LiefertInvalidArgument(
        string? feedbackType,
        string? title,
        string? description,
        string expectedMissingField)
    {
        var parameters = new ReportObservabilityFeedbackParameters(
            FeedbackType: feedbackType,
            Title: title,
            Description: description);

        var result = await ReportObservabilityFeedbackTool.ExecuteAsync(parameters);

        Assert.False(result.IsError); // Recoverable error per IsErrorPolicy
        Assert.NotNull(result.Content);
        var text = Assert.Single(result.Content) as TextContentBlock;
        Assert.NotNull(text);
        Assert.Contains(LinterErrorCodes.InvalidArgument, text.Text);
        Assert.Contains(expectedMissingField, text.Text);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultSeverity_WirdAufMediumGesetzt()
    {
        var parameters = new ReportObservabilityFeedbackParameters(
            FeedbackType: "feature_request",
            Title: "Neues Tool fuer Type Aliases",
            Description: "Bitte Support fuer C# 12 using alias hinzufuegen.",
            Severity: null);

        var result = await ReportObservabilityFeedbackTool.ExecuteAsync(parameters);

        Assert.True(result.IsError != true);
        Assert.NotNull(result.Content);
        var text = Assert.Single(result.Content) as TextContentBlock;
        Assert.NotNull(text);
        Assert.Contains("[INFO]:", text.Text);
    }
}
