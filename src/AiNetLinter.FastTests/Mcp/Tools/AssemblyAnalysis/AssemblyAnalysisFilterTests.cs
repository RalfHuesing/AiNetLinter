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
        var text = AssemblyAnalysisTestSupport.TextOf(result);
        Assert.Contains("Probe.Api.PublicApi", text, StringComparison.Ordinal);
        Assert.Contains("Save(bool abortOnWarning, ref int changeCount, [string mode])", text, StringComparison.Ordinal);
        Assert.Contains("Validate()", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveExtra", text, StringComparison.Ordinal);

        var metadataResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["ReadOnly", "Escaped", "NullDefault", "this[]"], 10),
            CancellationToken.None);
        var metadataText = AssemblyAnalysisTestSupport.TextOf(metadataResult);
        Assert.Contains("ReadOnly", metadataText, StringComparison.Ordinal);
        Assert.Contains("Escaped", metadataText, StringComparison.Ordinal);
        Assert.Contains("NullDefault", metadataText, StringComparison.Ordinal);
        Assert.Contains("property", metadataText, StringComparison.Ordinal);

        var limitedResult = await InspectAssemblyToolDispatch.ExecuteAsync(
            null,
            new InspectAssemblyArguments(assemblyPath, "Probe.Api", "PublicApi", null, true, 100, true, ["Save", "Validate"], 1),
            CancellationToken.None);
        var limitedText = AssemblyAnalysisTestSupport.TextOf(limitedResult);
        Assert.Contains("Save", limitedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Validate()", limitedText, StringComparison.Ordinal);
    }
}
