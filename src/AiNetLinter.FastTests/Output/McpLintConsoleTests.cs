#nullable enable

using System;
using System.IO;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.FastTests.Output;

/// <summary>
/// Unit-Tests fuer <see cref="McpLintConsole"/>: stellt sicher, dass die im MCP-Modus aktive
/// <see cref="ILintConsole"/>-Implementierung beide Methoden strukturell nach <c>stderr</c>
/// umleitet.
/// </summary>
[Trait("Category", "Unit")]
public sealed class McpLintConsoleTests
{
    [Fact]
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
    public void Instance_ReturnsSameSingleton()
    {
        Assert.Same(McpLintConsole.Instance, McpLintConsole.Instance);
    }
}
