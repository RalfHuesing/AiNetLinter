#nullable enable

using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.Output;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class ManagedAssemblyBinaryTests
{
    [Fact]
    public async Task InspectAssembly_AcceptsManagedExeWithoutExecutingIt()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-exe-");
        var assemblyPath = AssemblyTestHelper.EmitExecutable(temp, "ManagedExeProbe", """
            namespace Probe;
            public static class Program
            {
                public static void Main() { }
                public static string Describe() => "managed-exe";
            }
            """);

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, null, "Program", null, true, 100),
            CancellationToken.None);
        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);

        Assert.EndsWith(".exe", payload.AssemblyPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("ManagedExeProbe", payload.Identity?.Name);
        var program = Assert.Single(payload.Types);
        Assert.Contains(program.Members, member => member.Name == "Describe");
        Assert.Equal("complete", payload.Completeness);
    }

    [Fact]
    public async Task InspectAssembly_NativePeFailsWithTypedMetadataDiagnostic()
    {
        var nativeAssemblyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "kernel32.dll");
        Assert.True(File.Exists(nativeAssemblyPath), $"Native PE fixture fehlt: {nativeAssemblyPath}");

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(nativeAssemblyPath, null, null, null, true, 100),
            CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("keine .NET-Metadaten", AssemblyAnalysisTestSupport.TextOf(result), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("verwaltete .NET-.dll oder .exe mit IL", AssemblyAnalysisTestSupport.TextOf(result), StringComparison.Ordinal);
        var payload = JsonSerializer.Deserialize<McpErrorPayload>(
            result.StructuredContent!.Value.GetRawText(),
            McpJsonOptions.Default);
        Assert.NotNull(payload);
        Assert.Equal(LinterErrorCodes.WorkspaceDiagnostic, payload.Code);
        Assert.Equal(nativeAssemblyPath, payload.Context);
        Assert.Contains(".dll oder .exe mit IL", payload.Message, StringComparison.Ordinal);
        Assert.Equal(
            "Einmal erneut versuchen; bleibt der Fehler bestehen, Datei pruefen — Compile-Fehler blockieren Symbolaufloesung.",
            payload.Hint);
        Assert.True(payload.Recoverable);
    }

}
