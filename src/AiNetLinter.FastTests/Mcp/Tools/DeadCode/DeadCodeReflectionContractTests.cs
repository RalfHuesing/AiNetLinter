#nullable enable

using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

// @covers DeadCodeReflectionSelection
[Trait("Category", "Component")]
public sealed class DeadCodeReflectionContractTests
{
    [Fact]
    public async Task AssemblyScanWithUnknownFilterDoesNotClaimDefiniteActivation()
    {
        using var fixture = Create("""
            public interface IPlugin { }
            public sealed class Plugin : IPlugin { }
            public sealed class Orphan { }
            public static class Loader
            {
                public static void Load(System.Func<System.Type, bool> choose)
                {
                    foreach (var type in typeof(IPlugin).Assembly.GetTypes())
                        if (typeof(IPlugin).IsAssignableFrom(type) && choose(type)) System.Activator.CreateInstance(type);
                }
            }
            """);
        var result = await DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution,
            new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Class));
        Assert.Contains(result.UndecidableSymbols!, entry => entry.SymbolName == "Plugin");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task AttributeFilteredReflectionKeepsUnselectedProperty()
    {
        using var fixture = Create("""
            using System;
            public sealed class ColumnAttribute : Attribute { }
            public sealed class Row { [Column] public int Value { get; set; } public int Orphan { get; set; } }
            public static class Mapper
            {
                public static void Read(Row row)
                {
                    foreach (var property in typeof(Row).GetProperties())
                        if (property.IsDefined(typeof(ColumnAttribute), true)) Console.WriteLine(property.GetValue(row));
                }
            }
            """);
        var result = await Scan(fixture);
        Assert.Empty(result.DeadSymbols);
    }

    [Fact]
    public async Task DynamicReflectionFilterIsBoundedUncertainty()
    {
        using var fixture = Create("""
            public sealed class Row { public int Value { get; set; } }
            public sealed class Other { public int Value { get; set; } }
            public static class Mapper
            {
                public static object? Read(Row row, System.Func<System.Reflection.PropertyInfo, bool> choose)
                {
                    foreach (var property in typeof(Row).GetProperties())
                        if (choose(property)) return property.GetValue(row);
                    return null;
                }
            }
            """);
        var result = await Scan(fixture);
        Assert.Empty(result.DeadSymbols);
        Assert.Empty(result.UndecidableSymbols!);
    }

    private static RoslynTestSolution Create(string source) =>
        RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Reflection.slnx",
            new ProjectSpec("Host", [("Code.cs", source)]));

    private static Task<DeadCodeScanResult> Scan(RoslynTestSolution fixture) =>
        DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution, new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Property));
}
