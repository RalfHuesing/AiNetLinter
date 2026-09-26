#nullable enable

using System;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Component")]
public sealed class DeadCodeFalsePositiveRegressionTests
{
    [Fact]
    public async Task ScanAsync_ProductionEntryPointUsesMemberUnderTestPath_IsProductionUse()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [
                ("Features/Test/Counter.cs", """
                namespace Product;
                public sealed class Counter
                {
                    private int _generation;
                    public int Next() => ++_generation;
                }
                """),
                ("Program.cs", """
                namespace Product;
                public static class Program
                {
                    public static int Main() => new Counter().Next();
                }
                """)], OutputKind: Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
                VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.Field);

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "_generation");
    }

    [Fact]
    public async Task ScanAsync_MetadataOverrideUsedByFramework_IsNotDead()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("ReadableStream.cs", """
            namespace Product;
            public sealed class ReadableStream : System.IO.Stream
            {
                public override bool CanRead => true;
                public override bool CanSeek => false;
                public override bool CanWrite => false;
                public override long Length => 0;
                public override long Position { get; set; }
                public override int Read(byte[] buffer, int offset, int count) => 0;
                public override void Flush() { }
                public override long Seek(long offset, System.IO.SeekOrigin origin) => 0;
                public override void SetLength(long value) { }
                public override void Write(byte[] buffer, int offset, int count) { }
            }

            public static class StreamRunner
            {
                public static void Copy()
                {
                    using System.IO.Stream source = new ReadableStream();
                    using var destination = new System.IO.MemoryStream();
                    source.CopyTo(destination);
                }
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.Method);

        Assert.DoesNotContain(result.DeadSymbols, entry =>
            entry.ContainerType == "Product.ReadableStream" && entry.SymbolName == "Read");
    }

    [Fact]
    public async Task ScanAsync_ModuleInitializer_IsAProductionRootForItsType()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("ModuleHook.cs", """
            namespace Product;
            internal static class ModuleHook
            {
                [System.Runtime.CompilerServices.ModuleInitializer]
                internal static void Initialize() => RuntimeState.MarkInitialized();
            }

            internal static class RuntimeState
            {
                internal static void MarkInitialized() { }
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.All);

        Assert.DoesNotContain(result.DeadSymbols, entry =>
            entry.Kind == "class" && entry.SymbolName == "ModuleHook");
    }

    [Fact]
    public async Task ScanAsync_RecordKeyEquality_UsesPositionalProperty()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("Cache.cs", """
            namespace Product;
            public sealed class Cache
            {
                private readonly record struct Key(string Slug);
                private readonly System.Collections.Generic.Dictionary<Key, int> _values = new();

                public void Store(string slug) => _values[new Key(slug)] = 1;
                public bool Has(string slug) => _values.ContainsKey(new Key(slug));
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.Property);

        Assert.DoesNotContain(result.DeadSymbols, entry =>
            entry.ContainerType.Contains("Cache.Key", StringComparison.Ordinal) && entry.SymbolName == "Slug");
    }

    [Fact]
    public async Task ScanAsync_GenericReflectionMapper_UsesPropertiesWithoutSymbolReferences()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("Mapping.cs", """
            namespace Product;
            public sealed class Payload
            {
                public string Value { get; set; } = "";
            }

            public static class Mapper
            {
                public static string Map<T>(T value)
                {
                    var properties = typeof(T).GetProperties();
                    return string.Join(",", System.Array.ConvertAll(
                        properties,
                        property => property.GetValue(value)?.ToString()));
                }

                public static string Run(Payload payload) => Map(payload);
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.Property);

        Assert.DoesNotContain(result.DeadSymbols, entry =>
            entry.ContainerType == "Product.Payload" && entry.SymbolName == "Value");
    }

    [Fact]
    public async Task ScanAsync_PrivateFieldReadThroughReflection_IsNotDead()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("ReflectedState.cs", """
            namespace Product;
            public sealed class ReflectedState
            {
                private int _state = 42;

                public static int Read(ReflectedState value)
                {
                    var field = typeof(ReflectedState).GetField(
                        "_state",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);
                    return (int)field!.GetValue(value)!;
                }
            }
            """)], VirtualProjectDirectory: "src/Product"));

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.Field);

        Assert.DoesNotContain(result.DeadSymbols, entry => entry.SymbolName == "_state");
    }

    [Fact]
    public async Task ScanAsync_CompilerProvenUnreachableStatement_IsDeadCode()
    {
        using var testSolution = CreateSolution(
            new ProjectSpec("Product", [("Program.cs", """
            namespace Product;
            public static class Program
            {
                public static int Main()
                {
                    return 1;
                    System.Console.WriteLine("unreachable");
                }
            }
            """)], OutputKind: Microsoft.CodeAnalysis.OutputKind.ConsoleApplication,
                VirtualProjectDirectory: "src/Product"));

        var compilation = await Assert.Single(testSolution.Solution.Projects).GetCompilationAsync();
        Assert.Contains(compilation!.GetDiagnostics(), diagnostic => diagnostic.Id == "CS0162");

        var result = await ScanAsync(testSolution, DeadCodeKindFilter.All, DeadCodeMode.Both);

        Assert.Contains(result.DeadSymbols, entry =>
            entry.Reason.Contains("CS0162", StringComparison.Ordinal));
    }

    private static RoslynTestSolution CreateSolution(params ProjectSpec[] projects) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeFalsePositiveRegressionTests.slnx",
            projects);

    private static Task<DeadCodeScanResult> ScanAsync(
        RoslynTestSolution testSolution,
        DeadCodeKindFilter kind,
        DeadCodeMode mode = DeadCodeMode.Members) =>
        DeadCodeAdvisoryScanner.ScanAsync(
            testSolution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: kind,
                Mode: mode));
}
