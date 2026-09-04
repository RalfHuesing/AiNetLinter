#nullable enable

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Tools.TypeHierarchy;
using AiNetLinter.TestKit;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.TypeHierarchy;

[Trait("Category", "Component")]
public sealed class FindImplementationsTests
{
    [Fact]
    public async Task ExecuteAsync_InterfaceType_ReturnsAllImplementingClasses()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseProcessor", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("DerivedProcessor", textContent.Text, StringComparison.Ordinal);
        Assert.Contains("MoreDerivedProcessor", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(3, dto!.TotalCount);
        Assert.False(dto.IsTruncated);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("BaseProcessor", StringComparison.Ordinal));
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("DerivedProcessor", StringComparison.Ordinal));
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("MoreDerivedProcessor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_InterfaceMethod_ReturnsImplementingMethods()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor.Execute", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("BaseProcessor.Execute", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.NotEmpty(dto!.Implementations);
        Assert.Contains(dto.Implementations, i => i.MemberName == "Execute");
    }

    [Fact]
    public async Task ExecuteAsync_VirtualMethod_ReturnsOverrides()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "BaseProcessor.Execute", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("DerivedProcessor.Execute", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.True(dto!.TotalCount >= 1);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("DerivedProcessor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_AbstractClass_ReturnsDerivedClassesWithStatus()
    {
        var code = """
            namespace Shapes;

            public abstract class BaseShape
            {
                public abstract double Area();
            }

            public abstract class Polygon : BaseShape
            {
            }

            public class Circle : BaseShape
            {
                public override double Area() => 3.14;
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "Shapes", "Shapes.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "BaseShape", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(2, dto!.TotalCount);

        var polygon = Assert.Single(dto.Implementations, i => i.TypeName.Contains("Polygon", StringComparison.Ordinal));
        Assert.Equal("abstract", polygon.Status);

        var circle = Assert.Single(dto.Implementations, i => i.TypeName.Contains("Circle", StringComparison.Ordinal));
        Assert.Equal("concrete", circle.Status);
    }

    [Fact]
    public async Task ExecuteAsync_MaxResultsTruncates_ReturnsTruncatedResult()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "IProcessor", maxResults: 1, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("Ergebnis trunkiert", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.True(dto!.IsTruncated);
        Assert.Equal(3, dto.TotalCount);
        Assert.Single(dto.Implementations);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownSymbol_ReturnsSymbolNotFound()
    {
        using var fixture = new McpInMemoryTestContext(TransitiveSymbolGraphMiniSolutionSpec.Create());
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "NonExistentTypeXyz", maxResults: 50, ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("SYMBOL_NOT_FOUND", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NonVirtualMethod_ReturnsInvalidArgument()
    {
        var code = """
            namespace App;

            public class Service
            {
                public void NonVirtualMethod() {}
            }
            """;

        using var solution = RoslynTestSolutionFactory.CreateSolution(code, "App", "Service.cs");
        using var fixture = new McpInMemoryTestContext(solution);
        var server = fixture.CreateServer();

        var result = await FindImplementationsTool.ExecuteAsync(
            server, "Service.NonVirtualMethod", maxResults: 50, ct: CancellationToken.None);

        Assert.NotEqual(true, result.IsError);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("INVALID_ARGUMENT", textContent.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_AssemblyTarget_FindsImplementationsInAssembly()
    {
        using var temp = TestTempDirectory.Create("find-impl-asm-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "ImplProbe",
            "namespace Probe; public interface IGreeter { string Greet(); } public class EnglishGreeter : IGreeter { public string Greet() => \"Hello\"; }");

        await using var assemblyRegistry = new AssemblyAnalysisRegistry();
        var leaseResult = await assemblyRegistry.LeaseAsync(assemblyPath);
        Assert.NotNull(leaseResult.Lease);
        using var lease = leaseResult.Lease!;

        var result = await FindImplementationsTool.ExecuteAsync(
            lease.Server, "IGreeter", maxResults: 50, ct: CancellationToken.None);

        Assert.True(result.IsError is null or false);
        var textContent = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        Assert.Contains("EnglishGreeter", textContent.Text, StringComparison.Ordinal);

        var dto = JsonSerializer.Deserialize<FindImplementationsResultDto>(
            result.StructuredContent!.Value.GetRawText(), McpJsonOptions.Default);
        Assert.NotNull(dto);
        Assert.Equal(1, dto!.TotalCount);
        Assert.Contains(dto.Implementations, i => i.TypeName.Contains("EnglishGreeter", StringComparison.Ordinal));
    }
}
