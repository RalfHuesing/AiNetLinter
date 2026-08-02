using Xunit;
using System.CommandLine;
using AiNetLinter.Cli;

namespace AiNetLinter.Tests.Cli;

public sealed class IgnoreSuppressionsCliTests
{
    [Fact]
    public void IgnoreSuppressions_NotSet_ReturnsNull()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", "." });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.Null(parsedArgs.IgnoreSuppressions);
    }

    [Fact]
    public void IgnoreSuppressions_NoValue_DefaultsToAll()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", ".", "--ignore-suppressions" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        Assert.NotNull(parsedArgs.IgnoreSuppressions);
        Assert.Single(parsedArgs.IgnoreSuppressions!);
        Assert.Equal("all", parsedArgs.IgnoreSuppressions![0]);
    }

    [Fact]
    public void IgnoreSuppressions_AliasCSharp_NormalizesToCs()
    {
        var linterArgs = new LinterArgs
        {
            TargetPath = ".",
            Verbose = false,
            IgnoreSuppressions = new[] { "c#", "razor" }
        };

        var validationError = linterArgs.Validate();
        var normalized = linterArgs.GetNormalizedIgnoreSuppressions();

        Assert.Null(validationError);
        Assert.Equal(2, normalized.Count);
        Assert.Equal("cs", normalized[0]);
        Assert.Equal("razor", normalized[1]);
    }

    [Fact]
    public void IgnoreSuppressions_MultipleLanguages_NormalizesAndDeduplicates()
    {
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", ".", "--ignore-suppressions", "cs,razor", "js", "c#" });

        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);
        var linterArgs = new LinterArgs
        {
            TargetPath = parsedArgs.TargetPath,
            Verbose = false,
            IgnoreSuppressions = parsedArgs.IgnoreSuppressions
        };

        var validationError = linterArgs.Validate();
        var normalized = linterArgs.GetNormalizedIgnoreSuppressions();

        Assert.Null(validationError);
        Assert.Equal(3, normalized.Count);
        Assert.Contains("cs", normalized);
        Assert.Contains("razor", normalized);
        Assert.Contains("js", normalized);
    }

    [Fact]
    public void IgnoreSuppressions_InvalidLanguage_ReturnsValidationError()
    {
        var linterArgs = new LinterArgs
        {
            TargetPath = ".",
            Verbose = false,
            IgnoreSuppressions = new[] { "cs", "invalid_lang" }
        };

        var validationError = linterArgs.Validate();

        Assert.NotNull(validationError);
        Assert.Contains("Ungueltige Sprache fuer --ignore-suppressions: 'invalid_lang'", validationError);
    }
}
