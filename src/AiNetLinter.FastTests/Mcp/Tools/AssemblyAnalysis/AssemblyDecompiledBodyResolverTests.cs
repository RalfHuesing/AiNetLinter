#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Assemblies.Analysis.Bodies;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Unit")]
// @covers AssemblyDecompiledBodyResolver
public sealed class AssemblyDecompiledBodyResolverTests
{
    [Fact]
    public async Task ResolveAsync_DirectlyResolvesNamedTypesAndPropertyAccessors()
    {
        using var temp = TestTempDirectory.Create("assembly-body-resolver-direct-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "DirectBodyResolverProbe",
            """
            namespace Probe;
            public sealed class Document
            {
                private int number = 7;
                public int Number
                {
                    get { return number; }
                    set { number = value; }
                }
            }
            public struct Value { public int Number => 1; }
            public enum State { Ready }
            """);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        await using var session = new AssemblyAnalysisSession(
            assemblyPath,
            cacheRoot: temp.GetPath("cache"));

        Assert.Equal(AssemblySessionStatus.Complete, (await session.RefreshAsync()).Status);
        var compilation = session.CurrentGeneration!.Snapshot.Compilation;
        var resolver = AssemblyDecompiledBodyResolver.Create(
            assemblyPath,
            references,
            AssemblyDecompilationOptions.Default);
        var document = compilation.GetTypeByMetadataName("Probe.Document")!;
        var property = Assert.Single(document.GetMembers("Number").OfType<IPropertySymbol>());
        var value = compilation.GetTypeByMetadataName("Probe.Value")!;
        var state = compilation.GetTypeByMetadataName("Probe.State")!;

        var classBody = await resolver(document, 80, CancellationToken.None);
        var propertyBody = await resolver(property, 80, CancellationToken.None);
        var getterBody = await resolver(property.GetMethod!, 80, CancellationToken.None);
        var setterBody = await resolver(property.SetMethod!, 80, CancellationToken.None);
        var structBody = await resolver(value, 80, CancellationToken.None);
        var enumBody = await resolver(state, 80, CancellationToken.None);

        Assert.Equal("available", classBody.BodyAvailability);
        Assert.Contains("class Document", classBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", propertyBody.BodyAvailability);
        Assert.Contains("Number", propertyBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", getterBody.BodyAvailability);
        Assert.Contains("return number", getterBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", setterBody.BodyAvailability);
        Assert.Contains("number = value", setterBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", structBody.BodyAvailability);
        Assert.Contains("struct Value", structBody.Body, StringComparison.Ordinal);
        Assert.Equal("available", enumBody.BodyAvailability);
        Assert.Contains("enum State", enumBody.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResolveAsync_DirectlyReturnsTypedUnavailableForInterfacesAndAbstractMembers()
    {
        using var temp = TestTempDirectory.Create("assembly-body-resolver-unavailable-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "DirectBodyResolverUnavailableProbe",
            """
            namespace Probe;
            public interface IContract { void Run(); }
            public abstract class Base
            {
                public abstract void Run();
            }
            """);
        var references = new AssemblyReferenceResolver().Resolve(assemblyPath);
        await using var session = new AssemblyAnalysisSession(
            assemblyPath,
            cacheRoot: temp.GetPath("cache"));

        Assert.Equal(AssemblySessionStatus.Complete, (await session.RefreshAsync()).Status);
        var compilation = session.CurrentGeneration!.Snapshot.Compilation;
        var resolver = AssemblyDecompiledBodyResolver.Create(
            assemblyPath,
            references,
            AssemblyDecompilationOptions.Default);
        var contract = compilation.GetTypeByMetadataName("Probe.IContract")!;
        var abstractMethod = Assert.Single(
            compilation.GetTypeByMetadataName("Probe.Base")!.GetMembers("Run").OfType<IMethodSymbol>());

        var interfaceResult = await resolver(contract, 80, CancellationToken.None);
        var abstractResult = await resolver(abstractMethod, 80, CancellationToken.None);

        Assert.Equal("unavailable", interfaceResult.BodyAvailability);
        Assert.Equal("decompiledSignatureOnly", interfaceResult.ContentMode);
        Assert.Contains("Interfaces", interfaceResult.Hint, StringComparison.Ordinal);
        Assert.Equal("unavailable", abstractResult.BodyAvailability);
        Assert.Equal("decompiledSignatureOnly", abstractResult.ContentMode);
        Assert.Contains("abstract", abstractResult.Hint, StringComparison.Ordinal);
    }
}
