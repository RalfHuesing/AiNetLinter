#nullable enable

using System.Reflection;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Mcp;

[Collection("ConsoleTestCollection")]
public sealed class McpCodeGraphServerConstructorTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_TakesExactlyOneParameter_OfTypeMcpCodeGraphServerOptions()
    {
        var ctors = typeof(McpCodeGraphServer).GetConstructors(
            BindingFlags.Public | BindingFlags.Instance);

        Assert.Single(ctors);
        var parameters = ctors[0].GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(McpCodeGraphServerOptions), parameters[0].ParameterType);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_AcceptsNullOptions_ThrowsArgumentNullException()
    {
        // ArgumentNullException.ThrowIfNull wirft ggf. eine abgeleitete Exception je nach
        // .NET-Runtime-Version. Test akzeptiert daher die volle ArgumentException-Hierarchie.
        var ex = Assert.Throws<ArgumentNullException>(() => new McpCodeGraphServer(null!));
        Assert.Contains("options", ex.Message);
    }
}
