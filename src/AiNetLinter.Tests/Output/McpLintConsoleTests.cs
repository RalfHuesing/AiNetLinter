#nullable enable

using System.IO;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.Tests.Output;

/// <summary>
/// Unit-Tests fuer <see cref="McpLintConsole"/>: stellt sicher, dass die im MCP-Modus aktive
/// <see cref="ILintConsole"/>-Implementierung beide Methoden strukturell nach <c>stderr</c>
/// umleitet. Dieser Test ist die in-Memory-Entsprechung des E2E-Framing-Tests in
/// <c>McpServerCommandJsonRpcFramingTests</c>: der E2E-Test prueft das Verhalten am echten
/// Subprozess, dieser Unit-Test das Verhalten der Implementierung selbst.
/// </summary>
public sealed class McpLintConsoleTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void WriteLine_RoutesToStderr()
    {
        var original = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            McpLintConsole.Instance.WriteLine("test-mcp-stdout-line");

            Assert.Equal("test-mcp-stdout-line" + Environment.NewLine, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void WriteError_RoutesToStderr()
    {
        var original = Console.Error;
        var writer = new StringWriter();
        Console.SetError(writer);
        try
        {
            McpLintConsole.Instance.WriteError("test-mcp-stderr-line");

            Assert.Equal("test-mcp-stderr-line" + Environment.NewLine, writer.ToString());
        }
        finally
        {
            Console.SetError(original);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Instance_ReturnsSameSingleton()
    {
        Assert.Same(McpLintConsole.Instance, McpLintConsole.Instance);
    }
}
