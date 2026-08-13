#nullable enable

using System.Reflection;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
/// Strukturelle A3-Sicherung fuer den Konstruktor von <see cref="McpCodeGraphServer"/>:
/// Er nimmt genau einen Parameter vom Typ <see cref="McpCodeGraphServerOptions"/>. Eingefuehrt,
/// weil der Konstruktor am projektweiten <c>MaxConstructorDependencies: 5</c>-Limit
/// (siehe <c>AiNetLinter.mdc</c>) angelangt war und ein weiterer Parameter den Build gebrochen haette.
/// </summary>
[Trait("Category", "Unit")]
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
