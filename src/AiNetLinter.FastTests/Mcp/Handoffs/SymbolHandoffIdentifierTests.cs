#nullable enable

using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using System;
using System.Linq;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Handoffs;

[Trait("Category", "Unit")]
public sealed class SymbolHandoffIdentifierTests
{
    [Fact]
    public void Format_UsesTwoFixedLengthOpaqueTokensAndCanonicalDocumentationId()
    {
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\..\Assemblies\Probe.dll",
            new string('a', 64),
            generation: 42);

        var identifier = identity.Format("T:Probe.Type")!;
        var parts = identifier.Split(':', 5);

        Assert.Equal(5, parts.Length);
        Assert.Equal("i", parts[0]);
        Assert.Equal("1", parts[1]);
        Assert.Equal(22, parts[2].Length);
        Assert.Equal(22, parts[3].Length);
        Assert.All(parts[2..4].SelectMany(value => value), character =>
            Assert.True(char.IsLetterOrDigit(character) || character is '-' or '_'));
        Assert.Equal("T:Probe.Type", parts[4]);
        Assert.DoesNotContain("Probe.dll", identifier, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(new string('a', 64), identifier, StringComparison.Ordinal);
        Assert.DoesNotContain(":42:", identifier, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_IsDeterministicForEquivalentCanonicalTargetPaths()
    {
        var first = AnalysisSymbolIdentity.ForSource(
            @"C:\Workspace\src\..\workspace.slnx",
            new string('b', 64));
        var second = AnalysisSymbolIdentity.ForSource(
            @"C:\Workspace\workspace.slnx",
            new string('b', 64));

        Assert.Equal(first.Format("T:Probe.Type"), second.Format("T:Probe.Type"));
    }

    [Theory]
    [InlineData("Probe.Type")]
    [InlineData("M:Probe.Type#lf:Local")]
    [InlineData("X:Probe.Type")]
    public void Format_RejectsNonCanonicalDocumentationIds(string documentationCommentId)
    {
        var identity = AnalysisSymbolIdentity.ForSource(
            @"C:\Workspace\workspace.slnx",
            new string('b', 64));

        Assert.Null(identity.Format(documentationCommentId));
    }

    [Theory]
    [InlineData("i:2:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:T:Probe.Type")]
    [InlineData("i:1:aaaaaaaaaaaaaaaaaaaaaa=:bbbbbbbbbbbbbbbbbbbbbb:T:Probe.Type")]
    [InlineData("i:1:aaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:T:Probe.Type")]
    [InlineData("i:1:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:Probe.Type")]
    [InlineData("i:1:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:M:Probe.Type#lf:Local")]
    [InlineData("i:1:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:T:")]
    public void TryParse_RejectsEveryNonCanonicalHandoffForm(string value)
    {
        Assert.False(SymbolHandoffIdentifier.TryParse(value, out _));
    }

    [Fact]
    public void TryParse_ReturnsWireFieldsWithoutReconstructingPathOrFullHash()
    {
        var identity = AnalysisSymbolIdentity.ForSource(
            @"C:\Workspace\workspace.slnx",
            new string('c', 64));
        var value = identity.Format("M:Probe.Type.Run(System.Int32)")!;

        Assert.True(SymbolHandoffIdentifier.TryParse(value, out var parsed));
        Assert.Equal(SymbolHandoffOrigin.Source, parsed.Origin);
        Assert.Equal(22, parsed.TargetToken.Length);
        Assert.Equal(22, parsed.ContentToken.Length);
        Assert.Equal("M:Probe.Type.Run(System.Int32)", parsed.DocumentationCommentId);
    }
}
