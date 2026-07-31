using Xunit;
using System.CommandLine;
using AiNetLinter.Cli;

namespace AiNetLinter.Tests.Cli;

// @covers LinterArgs
public sealed class IgnoreSuppressionsCliTests
{
    [Fact]
    public void IgnoreSuppressions_NotSet_ReturnsNull()
    {
        // Arrange
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", "." });

        // Act
        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        // Assert
        Assert.Null(parsedArgs.IgnoreSuppressions);
    }

    [Fact]
    public void IgnoreSuppressions_NoValue_DefaultsToAll()
    {
        // Arrange
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", ".", "--ignore-suppressions" });

        // Act
        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);

        // Assert
        Assert.NotNull(parsedArgs.IgnoreSuppressions);
        Assert.Single(parsedArgs.IgnoreSuppressions!);
        Assert.Equal("all", parsedArgs.IgnoreSuppressions![0]);
    }

    [Fact]
    public void IgnoreSuppressions_AliasCSharp_NormalizesToCs()
    {
        // Arrange
        var linterArgs = new LinterArgs
        {
            TargetPath = ".",
            Verbose = false,
            IgnoreSuppressions = new[] { "c#", "razor" }
        };

        // Act
        var validationError = linterArgs.Validate();
        var normalized = linterArgs.GetNormalizedIgnoreSuppressions();

        // Assert
        Assert.Null(validationError);
        Assert.Equal(2, normalized.Count);
        Assert.Equal("cs", normalized[0]);
        Assert.Equal("razor", normalized[1]);
    }

    [Fact]
    public void IgnoreSuppressions_MultipleLanguages_NormalizesAndDeduplicates()
    {
        // Arrange
        var (root, options) = CliCommandBuilder.Build();
        var parseResult = root.Parse(new[] { "--path", ".", "--ignore-suppressions", "cs,razor", "js", "c#" });

        // Act
        var parsedArgs = CliCommandBuilder.Parse(parseResult, options);
        var linterArgs = new LinterArgs
        {
            TargetPath = parsedArgs.TargetPath,
            Verbose = false,
            IgnoreSuppressions = parsedArgs.IgnoreSuppressions
        };

        var validationError = linterArgs.Validate();
        var normalized = linterArgs.GetNormalizedIgnoreSuppressions();

        // Assert
        Assert.Null(validationError);
        Assert.Equal(3, normalized.Count);
        Assert.Contains("cs", normalized);
        Assert.Contains("razor", normalized);
        Assert.Contains("js", normalized);
    }

    [Fact]
    public void IgnoreSuppressions_InvalidLanguage_ReturnsValidationError()
    {
        // Arrange
        var linterArgs = new LinterArgs
        {
            TargetPath = ".",
            Verbose = false,
            IgnoreSuppressions = new[] { "cs", "invalid_lang" }
        };

        // Act
        var validationError = linterArgs.Validate();

        // Assert
        Assert.NotNull(validationError);
        Assert.Contains("Ungueltige Sprache fuer --ignore-suppressions: 'invalid_lang'", validationError);
    }
}
