#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.TypeResolution;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TypeResolution;

[Trait("Category", "Unit")]
public sealed class ResolveTypeOriginTests
{
    [Fact]
    public void ResolveTypeOrigin_ResolvesBclTypeFromMetadataReference()
    {
        var compilation = CreateTestCompilation("// empty");
        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation,
            "System.IDisposable",
            "dummy.csproj",
            null,
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var text = GetText(result);
        Assert.Contains("System.IDisposable", text);
        Assert.Contains("interface", text);
        Assert.Contains("Referenzierte Assembly", text);
    }

    [Fact]
    public void ResolveTypeOrigin_ResolvesLocalSourceType()
    {
        var code = """
            namespace App.Contracts;
            public interface IOrderProcessor
            {
                void Process();
            }
            """;
        var compilation = CreateTestCompilation(code);
        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation,
            "IOrderProcessor",
            "C:/App/App.csproj",
            null,
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var text = GetText(result);
        Assert.Contains("App.Contracts.IOrderProcessor", text);
        Assert.Contains("interface", text);
        Assert.Contains("Projekt-Quellcode", text);
        Assert.Contains("C:/App/App.csproj", text);

        using var payload = JsonDocument.Parse(result.StructuredContent!.Value.GetRawText());
        var origin = payload.RootElement.GetProperty("resolveTypeOrigin").GetProperty("origin");
        Assert.Equal("C:/App/App.csproj", origin.GetProperty("targetPath").GetString());
        Assert.False(origin.TryGetProperty("projectName", out _));
        Assert.Equal(1, origin.GetProperty("sourceLocations").GetArrayLength());
        Assert.Equal("source", origin.GetProperty("assemblyOrigin").GetString());
        Assert.False(origin.TryGetProperty("outputAssembly", out _));
    }

    [Fact]
    public void ResolveTypeOrigin_ResolvesTypeFromReferencedExternalDll()
    {
        using var temp = TestTempDirectory.Create("resolve-type-ref-");
        var depCode = """
            namespace Vendor.Data;
            public interface IDataProvider
            {
                string GetData();
            }
            """;
        var depDllPath = AssemblyTestHelper.EmitAssembly(temp, "Vendor.Data", depCode);

        var compilation = CreateTestCompilation("// consumer", [MetadataReference.CreateFromFile(depDllPath)]);
        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation,
            "IDataProvider",
            "C:/App/Consumer.csproj",
            null,
            CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var text = GetText(result);
        Assert.Contains("Vendor.Data.IDataProvider", text);
        Assert.Contains("Vendor.Data", text);
        Assert.Contains(depDllPath, text);
        Assert.Contains("interface", text);
        Assert.Contains("Referenzierte Assembly", text);
    }

    [Fact]
    public void ResolveTypeOrigin_SourcePartialTypeReportsEverySourceLocation()
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        var compilation = CSharpCompilation.Create(
            "App",
            [
                CSharpSyntaxTree.ParseText("namespace App; public partial class Split {}", path: @"C:\App\One.cs"),
                CSharpSyntaxTree.ParseText("namespace App; public partial class Split {}", path: @"C:\App\Two.cs"),
            ],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation, "App.Split", @"C:\App\App.slnx", null, CancellationToken.None);

        using var payload = JsonDocument.Parse(result.StructuredContent!.Value.GetRawText());
        var locations = payload.RootElement
            .GetProperty("resolveTypeOrigin").GetProperty("origin").GetProperty("sourceLocations");
        Assert.Equal(2, locations.GetArrayLength());
        Assert.Equal(@"C:\App\One.cs", locations[0].GetProperty("path").GetString());
        Assert.Equal(@"C:\App\Two.cs", locations[1].GetProperty("path").GetString());
    }

    [Fact]
    public void ResolveTypeOrigin_UnresolvedType_ReturnsRecoverableSymbolNotFound()
    {
        var compilation = CreateTestCompilation("// empty");
        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation,
            "NonExistentType404",
            "dummy.csproj",
            null,
            CancellationToken.None);

        Assert.NotNull(result);
        var text = GetText(result);
        Assert.Contains("SYMBOL_NOT_FOUND", text);
        Assert.Contains("NonExistentType404", text);
        Assert.Contains("Durchsuchte Referenzen", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveTypeOrigin_EmptyTypeName_ReturnsInvalidArgument(string emptyName)
    {
        var compilation = CreateTestCompilation("// empty");
        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation,
            emptyName,
            "dummy.csproj",
            null,
            CancellationToken.None);

        Assert.NotNull(result);
        var text = GetText(result);
        Assert.Contains("INVALID_ARGUMENT", text);
        Assert.Contains("typeName darf nicht leer sein", text);
    }

    [Fact]
    public async Task ResolveTypeOrigin_AssemblyTarget_ResolvesViaLease()
    {
        using var temp = TestTempDirectory.Create("resolve-type-lease-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetProbe",
            "namespace Target; public class TargetWorker { public void Do() { } }");

        await using var registry = new AssemblyAnalysisRegistry();
        var leaseResult = await registry.LeaseAsync(assemblyPath);
        Assert.NotNull(leaseResult.Lease);
        using var lease = leaseResult.Lease!;

        var result = await ResolveTypeOriginTool.ExecuteAssemblyAsync(lease, "TargetWorker", CancellationToken.None);
        Assert.True(result.IsError is null or false);
        var text = GetText(result);
        Assert.Contains("Target.TargetWorker", text);
        Assert.Contains("class", text);
        Assert.Contains("TargetProbe", text);
        Assert.Contains("Dekompilierte Assembly", text);

        using var payload = JsonDocument.Parse(result.StructuredContent!.Value.GetRawText());
        var origin = payload.RootElement.GetProperty("resolveTypeOrigin").GetProperty("origin");
        Assert.Equal(assemblyPath, origin.GetProperty("targetPath").GetString());
        Assert.False(origin.TryGetProperty("projectName", out _));
        Assert.Equal("assembly", origin.GetProperty("assemblyOrigin").GetString());
        Assert.Equal(assemblyPath, origin.GetProperty("outputAssembly").GetString());
    }

    [Fact]
    public void ResolveTypeOrigin_ResolvesUnqualifiedGenericType()
    {
        var compilation = CreateTestCompilation("public class Worker { }");

        var result = ResolveTypeOriginTool.ExecuteCompilation(
            compilation, "Action<T>", "dummy.cs", null, CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var text = GetText(result);
        Assert.Contains("System.Action<T>", text);
        Assert.Contains("delegate", text);
    }

    private static Compilation CreateTestCompilation(string source, MetadataReference[]? additionalRefs = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        MetadataReference[] references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.IDisposable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
        ];

        if (additionalRefs is not null && additionalRefs.Length > 0)
        {
            references = references.Concat(additionalRefs).ToArray();
        }

        return CSharpCompilation.Create(
            "TestAssembly",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string GetText(CallToolResult result)
    {
        var block = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        return block.Text;
    }
}
