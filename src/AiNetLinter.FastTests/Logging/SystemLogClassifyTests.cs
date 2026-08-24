#nullable enable

using AiNetLinter.Logging;
using Xunit;

namespace AiNetLinter.FastTests.Logging;

[Trait("Category", "Unit")]
public sealed class SystemLogClassifyTests
{
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
