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
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Assembly: `ManagedExeProbe`", text, StringComparison.Ordinal);
        Assert.Contains(".exe", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Describe()", text, StringComparison.Ordinal);
        Assert.Contains("Vollständigkeit: `complete`", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InspectAssembly_NativePeFailsWithTypedInvalidAssemblyDiagnostic()
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
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains(LinterErrorCodes.InvalidAssembly, text, StringComparison.Ordinal);
        Assert.Contains(".dll oder .exe mit IL", text, StringComparison.Ordinal);
    }

}
