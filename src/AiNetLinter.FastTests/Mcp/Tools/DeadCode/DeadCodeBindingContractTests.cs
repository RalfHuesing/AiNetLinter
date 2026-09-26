#nullable enable

using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

// @covers DeadCodeFrameworkUsage
// @covers DeadCodeMarkupUsage
[Trait("Category", "Component")]
public sealed class DeadCodeBindingContractTests
{
    [Fact]
    public async Task RecordEqualityDoesNotReadComputedProperty()
    {
        using var fixture = Create("""
            public sealed record Key(int Id) { public int Orphan => 1; }
            public static class Runner { public static bool Equal(Key a, Key b) => a == b; }
            """);
        var result = await Scan(fixture, DeadCodeKindFilter.Property);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Id");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task ConfigurationBinderProtectsWritableContractOnly()
    {
        using var fixture = Create("""
            using Microsoft.Extensions.Configuration;
            public sealed class Settings { public int Port { get; set; } private int Orphan { get; set; } }
            public sealed class Other { public int Port { get; set; } }
            public static class Startup { public static Settings? Read(IConfiguration config) => config.Get<Settings>(); }
            """, MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Configuration.ConfigurationBinder).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Configuration.IConfiguration).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location));
        var result = await Scan(fixture, DeadCodeKindFilter.Property);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ContainerType == "Settings" && entry.SymbolName == "Port");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
        Assert.Contains(result.DeadSymbols, entry => entry.ContainerType == "Other" && entry.SymbolName == "Port");
    }

    [Fact]
    public async Task ConditionalJsonIgnoreStillBindsProperty()
    {
        using var fixture = Create("""
            using System.Text.Json;
            using System.Text.Json.Serialization;
            public sealed class Payload
            {
                [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Value { get; set; }
                [JsonIgnore] public int Orphan { get; set; }
            }
            public static class Runner { public static string Run(Payload value) => JsonSerializer.Serialize(value); }
            """, MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location));
        var result = await Scan(fixture, DeadCodeKindFilter.Property);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Value");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task SerializerBindsContractButNotUnrelatedOrIgnoredProperty()
    {
        using var fixture = Create("""
            using System.Text.Json;
            using System.Text.Json.Serialization;
            public sealed class Payload
            {
                public int Value { get; set; }
                [JsonIgnore] public int Ignored { get; set; }
            }
            public sealed class Other { public int Value { get; set; } }
            public static class Runner
            {
                public static string Run(Payload value) => JsonSerializer.Serialize(value);
            }
            """, MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
            MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location));
        var result = await Scan(fixture, DeadCodeKindFilter.Property);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ContainerType == "Payload" && entry.SymbolName == "Value");
        Assert.Contains(result.DeadSymbols, entry => entry.ContainerType == "Other" && entry.SymbolName == "Value");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Ignored");
    }

    [Fact]
    public async Task SameNamedAttributeDoesNotProtectMethod()
    {
        using var fixture = Create("""
            public sealed class ModuleInitializerAttribute : System.Attribute { }
            public sealed class ParameterAttribute : System.Attribute { }
            public sealed class Worker
            {
                [ModuleInitializer] public void Orphan() { }
                [Parameter] public void AnotherOrphan() { }
            }
            """);
        var result = await Scan(fixture, DeadCodeKindFilter.Method);
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "AnotherOrphan");
    }

    [Fact]
    public async Task OrphanTypeIsOneGroupAndInternalCallsDoNotKeepItAlive()
    {
        using var fixture = Create("""
            public sealed partial class Orphan
            {
                public void Wrap() => Target();
                private void Target() { }
            }
            public sealed partial class Orphan { public int Value { get; set; } }
            """);
        var result = await Scan(fixture, DeadCodeKindFilter.All);
        var group = Assert.Single(result.DeadSymbols);
        Assert.Equal("Orphan", group.SymbolName);
        Assert.Equal("class", group.Kind);
    }

    [Fact]
    public async Task ScopeExcludesConstructorsEventsIndexersAndEnumValues()
    {
        using var fixture = Create("""
            public enum Choice { One, Two }
            public sealed class Worker
            {
                public Worker() { }
                public event System.Action? Changed;
                public int this[int index] => 0;
                public void Orphan() { }
            }
            public static class Runner { public static void Run(Worker worker, Choice choice) { } }
            """);
        var result = await Scan(fixture, DeadCodeKindFilter.All);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.Kind is "constructor" or "event" || entry.SymbolName is "this[]" or "One" or "Two");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    [Fact]
    public async Task RecordEqualityReadsNonPositionalPropertiesButConstructionDoesNot()
    {
        using var fixture = Create("""
            public sealed record Key(int Id) { public int Extra { get; init; } }
            public sealed record Written(int Id) { public int Extra { get; init; } }
            public static class Runner
            {
                public static bool Equal(Key left, Key right) => left == right;
                public static Written Make() => new Written(1) { Extra = 2 };
            }
            """);
        var result = await Scan(fixture, DeadCodeKindFilter.Property);
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.ContainerType == "Key");
        Assert.Contains(result.DeadSymbols, entry => entry.ContainerType == "Written" && entry.SymbolName == "Id");
        Assert.Contains(result.DeadSymbols, entry => entry.ContainerType == "Written" && entry.SymbolName == "Extra");
    }

    [Fact]
    public async Task XamlStaticBindingProtectsOnlyResolvedMember()
    {
        using var fixture = Create("""
            namespace Ui;
            public static class Texts { public const string Back = "Back"; public const string Orphan = "Back"; }
            """);
        var project = fixture.Solution.Projects.Single();
        var withMarkup = project.AddAdditionalDocument("View.xaml", """
            <Window xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:ui="clr-namespace:Ui">
                <Button Content="{x:Static ui:Texts.Back}" />
            </Window>
            """).Project.Solution;
        var result = await DeadCodeAdvisoryScanner.ScanAsync(withMarkup,
            new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: DeadCodeKindFilter.Field));
        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "Back");
        Assert.Contains(result.DeadSymbols, entry => entry.SymbolName == "Orphan");
    }

    private static RoslynTestSolution Create(string source, params MetadataReference[] references) =>
        RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\Bindings.slnx",
            new ProjectSpec("Host", [("Code.cs", source)], AdditionalReferences: references));

    private static Task<DeadCodeScanResult> Scan(RoslynTestSolution fixture, DeadCodeKindFilter kind) =>
        DeadCodeAdvisoryScanner.ScanAsync(fixture.Solution, new(Accessibility: DeadCodeAccessibilityFilter.All, Kind: kind));
}
