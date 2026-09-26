using Microsoft.CodeAnalysis;
using Microsoft.JSInterop;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Tools.Verify.DeadCode;

namespace AiNetLinter.FastTests.Mcp.Tools.DeadCode;

[Trait("Category", "Unit")]
public sealed class DeadCodeEntryPointAttributeTests
{
    [Fact]
    public async Task ScanAsync_ModuleInitializer_ProtectsMethodAndDeclaringType()
    {
        using var solution = CreateSolution(
            """
            using System.Runtime.CompilerServices;

            internal class StartupHook
            {
                [ModuleInitializer]
                internal static void Initialize() { }
            }
            """);

        var result = await ScanAsync(solution);

        Assert.DoesNotContain(result.DeadSymbols, item => item.SymbolName is "StartupHook" or "Initialize");
    }

    [Fact]
    public async Task ScanAsync_JsInvokable_ProtectsMethodAndDeclaringType()
    {
        using var solution = CreateSolution(
            """
            public class JsCallbacks
            {
                [Microsoft.JSInterop.JSInvokable]
                public void InvokeFromJavaScript() { }
            }
            """,
            MetadataReference.CreateFromFile(typeof(JSInvokableAttribute).Assembly.Location));
        var result = await ScanAsync(solution);

        Assert.DoesNotContain(result.DeadSymbols, item => item.SymbolName is "JsCallbacks" or "InvokeFromJavaScript");
    }

    [Fact]
    public async Task ScanAsync_ConfiguredEntryPointAttribute_IsAdditiveToDefaults()
    {
        using var solution = CreateSolution(
            """
            using System.Runtime.CompilerServices;

            namespace Microsoft.SemanticKernel
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class KernelFunctionAttribute : System.Attribute { }
            }

            internal class StartupHook
            {
                [ModuleInitializer]
                internal static void Initialize() { }
            }

            public class SemanticKernelPlugin
            {
                [Microsoft.SemanticKernel.KernelFunction]
                public void Invoke() { }
            }
            """);
        var config = TestHelper.CreateDefaultConfig() with
        {
            DeadCode = new DeadCodeConfig
            {
                EntryPointAttributes = ["Microsoft.SemanticKernel.KernelFunctionAttribute"],
            },
        };

        var result = await ScanAsync(solution, config);

        Assert.DoesNotContain(result.DeadSymbols, item => item.SymbolName is "StartupHook" or "Initialize");
        Assert.DoesNotContain(result.DeadSymbols, item => item.SymbolName is "SemanticKernelPlugin" or "Invoke");
    }

    [Fact]
    public async Task ScanAsync_UnconfiguredAttributeAndForeignSameNameAttribute_DoNotProtectSymbols()
    {
        using var solution = CreateSolution(
            """
            namespace Microsoft.SemanticKernel
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class KernelFunctionAttribute : System.Attribute { }
            }

            namespace OtherFramework
            {
                [System.AttributeUsage(System.AttributeTargets.Method)]
                public sealed class JSInvokableAttribute : System.Attribute { }
            }

            public class UnconfiguredPlugin
            {
                [Microsoft.SemanticKernel.KernelFunction]
                public void Run() { }
            }

            public class ForeignCallbacks
            {
                [OtherFramework.JSInvokable]
                public void Invoke() { }
            }
            """);

        var result = await ScanAsync(solution);

        Assert.Contains(result.DeadSymbols, item => item.SymbolName is "UnconfiguredPlugin" or "Run");
        Assert.Contains(result.DeadSymbols, item => item.SymbolName is "ForeignCallbacks" or "Invoke");
    }

    private static RoslynTestSolution CreateSolution(string source, params MetadataReference[] additionalReferences) =>
        RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\DeadCodeEntryPointAttributeTests.slnx",
            new ProjectSpec("EntryPointApp", [("EntryPoints.cs", source)], AdditionalReferences: additionalReferences, VirtualProjectDirectory: "."));

    private static Task<DeadCodeScanResult> ScanAsync(RoslynTestSolution solution, Config? config = null) =>
        DeadCodeAdvisoryScanner.ScanAsync(
            solution.Solution,
            new DeadCodeAdvisoryOptions(
                Accessibility: DeadCodeAccessibilityFilter.All,
                Confidence: DeadCodeConfidenceFilter.Both,
                Kind: DeadCodeKindFilter.All,
                Config: config ?? TestHelper.CreateDefaultConfig()),
            CancellationToken.None);
}
