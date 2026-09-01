#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis.Dispatch;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
public sealed class AssemblyAnalysisFilterTests
{
    [Fact]
    public async Task InspectAssembly_SupportsExactTypeMultipleMemberFiltersAndParameterDetails()
    {
        using var temp = TestTempDirectory.Create("assembly-analysis-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "FilterProbe", """
            namespace Probe.Api;
            public sealed class PublicApi
            {
                public bool Save(bool abortOnWarning, ref int changeCount, string mode = "safe") => true;
                public static ref readonly int ReadOnly(ref readonly int value) => ref value;
                public void Escaped(string value = "line\n\t", char quote = '\'') { }
                public void NullDefault(string? value = null) { }
                public string this[int index] => index.ToString();
                public void SaveExtra() { }
                public bool Validate() => true;
            }
            public sealed class PublicApiHelper
            {
                public void Save() { }
            }
            """);

        var result = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["Save", "Validate"], 10),
            CancellationToken.None);
        var payload = AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(result);

        var type = Assert.Single(payload.Types);
        Assert.Equal(2, type.TotalMembers);
        Assert.False(type.MembersTruncated);
        Assert.DoesNotContain(type.Members, member => member.Name == "SaveExtra");
        var save = Assert.Single(type.Members, member => member.Name == "Save");
        Assert.Contains("abortOnWarning", save.Signature, StringComparison.Ordinal);
        Assert.Equal(
            ["abortOnWarning", "changeCount", "mode"],
            save.Parameters.Select(parameter => parameter.Name).ToArray());
        Assert.Equal("bool", save.Parameters[0].Type);
        Assert.Equal("none", save.Parameters[0].RefKind);
        Assert.Equal("ref", save.Parameters[1].RefKind);
        Assert.True(save.Parameters[2].IsOptional);
        Assert.Equal("\"safe\"", save.Parameters[2].DefaultValue);

        var metadataResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["ReadOnly", "Escaped", "NullDefault", "this[]"], 10),
            CancellationToken.None);
        var metadataType = Assert.Single(AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(metadataResult).Types);
        var readOnly = Assert.Single(metadataType.Members, member => member.Name == "ReadOnly");
        Assert.Equal("ref readonly", readOnly.Parameters[0].RefKind);
        var escaped = Assert.Single(metadataType.Members, member => member.Name == "Escaped");
        Assert.Equal("\"line\\n\\t\"", escaped.Parameters[0].DefaultValue);
        Assert.Equal("'\\''", escaped.Parameters[1].DefaultValue);
        var nullDefault = Assert.Single(metadataType.Members, member => member.Name == "NullDefault");
        Assert.Equal("null", nullDefault.Parameters[0].DefaultValue);
        var indexer = Assert.Single(metadataType.Members, member => member.Name == "this[]");
        Assert.Equal("property", indexer.Kind);
        Assert.Equal("index", indexer.Parameters[0].Name);
        Assert.Equal("int", indexer.Parameters[0].Type);

        var limitedResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["Save", "Validate"], 1),
            CancellationToken.None);
        var limitedType = Assert.Single(AssemblyAnalysisTestSupport.Deserialize<InspectAssemblyPayload>(limitedResult).Types);
        Assert.Single(limitedType.Members);
        Assert.Equal(2, limitedType.TotalMembers);
        Assert.True(limitedType.MembersTruncated);
    }
}
