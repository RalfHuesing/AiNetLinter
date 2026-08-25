#nullable enable

using System.Collections.Generic;
using AiNetLinter.Logging;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Logging;

[Trait("Category", "Unit")]
public sealed class SystemLogClassifyTests
{
    [Fact]
    public void ExtractErrorCode_LiestNurDenStrukturiertenFehlerkopf()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = "[ERROR]: INVALID_ARGUMENT: Parameter fehlt\n  hint: pruefen" }],
        };

        Assert.Equal("INVALID_ARGUMENT", McpCallLoggingFilter.ExtractErrorCode(result));
    }

    [Fact]
    public void ExtractErrorCode_FaelltBeiUnstrukturiertemFehlerAufFallbackZurueck()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = "unerwarteter Fehler" }],
        };

        Assert.Equal(McpCallLoggingFilter.UnknownErrorCode, McpCallLoggingFilter.ExtractErrorCode(result));
    }

    [Fact]
    public void AnalyzeResult_RecoverableError_ExtrahierErrorCodeUndMessage()
    {
        var result = new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "[ERROR]: SYMBOL_NOT_FOUND: Kein Symbol gefunden fuer Identifikator 'Foo'.\n  hint: find_symbol" }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.RecoverableError, details.Status);
        Assert.Equal("SYMBOL_NOT_FOUND", details.ErrorCode);
        Assert.Equal("Kein Symbol gefunden fuer Identifikator 'Foo'.", details.ErrorMessage);
    }

    [Fact]
    public void AnalyzeResult_RecoverableError_MitFuehrendenLeerzeilen_ExtrahierErrorCode()
    {
        var result = new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "\n  \n[ERROR]: INVALID_ARGUMENT: Pflichtparameter fehlt." }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.RecoverableError, details.Status);
        Assert.Equal("INVALID_ARGUMENT", details.ErrorCode);
        Assert.Equal("Pflichtparameter fehlt.", details.ErrorMessage);
    }

    [Fact]
    public void AnalyzeResult_ProtocolError_ExtrahierErrorCodeUndMessage()
    {
        var result = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = "[ERROR]: SOLUTION_NOT_LOADED: Solution ist nicht geladen\n  hint: Server-Log pruefen" }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.ProtocolError, details.Status);
        Assert.Equal("SOLUTION_NOT_LOADED", details.ErrorCode);
        Assert.Equal("Solution ist nicht geladen", details.ErrorMessage);
    }

    [Fact]
    public void AnalyzeResult_Loading_ErkenntLadezustand()
    {
        var result = new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "[INFO]: Server laedt die Solution noch. Bitte in wenigen Sekunden erneut versuchen." }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.Loading, details.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("0 Treffer fuer das angegebene Pattern.")]
    [InlineData("Keine Symbole gefunden.")]
    [InlineData("Keine Code-Duplikate gefunden.")]
    [InlineData("Keine Hotspots gefunden.")]
    [InlineData("Keine Regelverstöße gefunden.")]
    [InlineData("Keine Dateien im Scope.")]
    [InlineData("0 Verstöße gefunden.")]
    [InlineData("0 Verstoesse gefunden.")]
    [InlineData("0 Referenzen")]
    [InlineData("0 Duplikate")]
    [InlineData("0 Funde")]
    [InlineData("0 betroffene Symbole")]
    [InlineData("0 Kanten")]
    [InlineData("0 Magic Values")]
    [InlineData("0 Symbole")]
    [InlineData("[INFO]: Suche in Scope 'src' ausgefuehrt.\n0 Treffer gefunden.")]
    public void AnalyzeResult_EmptyResults_ErkenntNullTreffer(string text)
    {
        var result = new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = text }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.Empty, details.Status);
    }

    [Fact]
    public void AnalyzeResult_Success_NormalerErgebnisText()
    {
        var result = new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = "src/Foo.cs:10 - class: Foo\nsrc/Bar.cs:20 - class: Bar" }],
        };

        var details = McpCallLoggingFilter.AnalyzeResult(result);
        Assert.Equal(McpCallStatus.Success, details.Status);
        Assert.Null(details.ErrorCode);
    }

    [Fact]
    public void FormatArguments_VerschiedeneEingaben_KompaktesJson()
    {
        Assert.Equal("{}", McpCallLoggingFilter.FormatArguments(null));
        Assert.Equal("{}", McpCallLoggingFilter.FormatArguments(new Dictionary<string, object?>()));

        var dict = new Dictionary<string, object?>
        {
            ["namePattern"] = "TestService",
            ["maxResults"] = 10,
        };
        var formatted = McpCallLoggingFilter.FormatArguments(dict);
        Assert.Contains("namePattern", formatted);
        Assert.Contains("TestService", formatted);
        Assert.Contains("10", formatted);
    }

    [Theory]
    [InlineData("[WARN]: Irgendwas ist aufgefallen", Serilog.Events.LogEventLevel.Warning)]
    [InlineData("[ERROR]: SOLUTION_NOT_FOUND: fehlt", Serilog.Events.LogEventLevel.Error)]
    [InlineData("[FATAL ERROR]: boom", Serilog.Events.LogEventLevel.Fatal)]
    [InlineData("[INFO]: Server laedt die Solution noch.", Serilog.Events.LogEventLevel.Information)]
    public void Classify_BekanntePraefixe_Zuordnen(string message, Serilog.Events.LogEventLevel expected)
    {
        Assert.Equal(expected, SystemLog.Classify(message));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Linter: 3 Verstoesse in 2 Dateien")]
    public void Classify_OhneDiagnosePraefix_Null(string message)
    {
        Assert.Null(SystemLog.Classify(message));
    }
}
