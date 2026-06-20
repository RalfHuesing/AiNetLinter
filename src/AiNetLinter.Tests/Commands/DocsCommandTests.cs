#nullable enable

using System;
using System.IO;
using System.Text;
using Xunit;
using AiNetLinter.Commands;

namespace AiNetLinter.Tests.Commands;

/// <summary>
/// Tests für <see cref="DocsCommand"/>.
/// </summary>
public sealed class DocsCommandTests
{
    [Theory]
    [InlineData("readme")]
    [InlineData("agent-api")]
    [InlineData("configuration")]
    [InlineData("rationale")]
    [InlineData("roadmap")]
    [InlineData("rules-json")]
    [InlineData("Readme")]
    [InlineData("AGENT-API")]
    [InlineData("Configuration")]
    [InlineData("roadmap ")]
    public void Run_WithValidDocs_ReturnsZeroAndWritesContent(string docName)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            var result = DocsCommand.Run(docName);
            Assert.Equal(0, result);
            var output = writer.ToString();
            Assert.False(string.IsNullOrWhiteSpace(output));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Run_WithNullOrEmpty_ReturnsOneAndWritesError(string? docName)
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            var result = DocsCommand.Run(docName);
            Assert.Equal(1, result);
            var error = writer.ToString();
            Assert.Contains("[ERROR]: --docs benötigt den Namen eines Dokuments.", error);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Run_WithInvalidDocName_ReturnsOneAndWritesErrorAndAvailableDocs()
    {
        var originalError = Console.Error;
        var originalOut = Console.Out;
        using var errWriter = new StringWriter();
        using var outWriter = new StringWriter();
        Console.SetError(errWriter);
        Console.SetOut(outWriter);
        try
        {
            var result = DocsCommand.Run("invalid-doc-name");
            Assert.Equal(1, result);
            
            var error = errWriter.ToString();
            Assert.Contains("[ERROR]: Dokumentation 'invalid-doc-name' wurde nicht gefunden.", error);
            
            var output = outWriter.ToString();
            Assert.Contains("Verfügbare Dokumente:", output);
            Assert.Contains("- readme", output);
            Assert.Contains("- agent-api", output);
            Assert.Contains("- configuration", output);
            Assert.Contains("- rationale", output);
            Assert.Contains("- roadmap", output);
            Assert.Contains("- rules-json", output);
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOut);
        }
    }
}
