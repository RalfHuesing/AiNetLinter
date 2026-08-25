#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.MagicValues;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

/// <summary>
/// Verifikationstests für die False-Positive-Reduktion und das Holder-Bewusstsein in
/// <see cref="FindMagicValuesScanner"/> (Maßnahmen M1 bis M6). Deckt den 12-Punkte-Testkatalog
/// aus dem Konzept isoliert und deterministisch ab.
/// </summary>
[Trait("Category", "Component")]
public sealed class FindMagicValuesScannerFalsePositiveTests
{
    [Fact]
    public async Task Classify_DateFormatString_OrdinaryWordsWithSubstrings_AreNotReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string Pwd = ""Password"";
    public const string Msg = ""Message"";
    public const string Dll = ""System.Collections.Immutable.dll"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.ConstantCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_DateFormatString_PurePatterns_AreReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string F1 = ""yyyy-MM-dd"";
    public const string F2 = ""yyyyMMddHHmmss"";
    public const string F3 = ""ddd, dd MMM yyyy HH:mm:ss"";
    public const string F4 = ""{0:F2}"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.ConstantCandidates);

        Assert.Equal(4, result.Payload!.MagicValues.Count);
        Assert.All(result.Payload!.MagicValues, e => Assert.Equal("constant_candidates", e.Category));
    }

    [Fact]
    public async Task Classify_SecurityCandidate_IdentifiersLikePublicKeyToken_AreNotSecurityCandidates()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const string T1 = ""publicKeyToken"";
    public const string T2 = ""CancellationToken"";
    public const string T3 = ""AuthenticationStateProvider"";
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.SecurityCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_SecurityCandidate_ExactKeywordOrPrefix_AreReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public void M()
    {
        Connect(""password"");
        Connect(""sk-1234567890abcdef"");
    }
    private void Connect(string secret) { }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.SecurityCandidates);

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.All(result.Payload!.MagicValues, e => Assert.Equal("security_candidates", e.Category));
    }

    [Fact]
    public async Task Classify_HttpStatusCode_NumbersWithoutStatusContext_AreNotReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public const int PageSize = 200;
    public const int MaxTimeoutSeconds = 300;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_HttpStatusCode_StatusContext_AreReportedAsStandardCandidates()
    {
        const string source = @"
namespace Test;
public sealed class HttpResponse
{
    public int StatusCode { get; set; }
}
public sealed class Foo
{
    public void M(HttpResponse response, int status)
    {
        if (response.StatusCode == 404) { }
        switch (status)
        {
            case 500: break;
        }
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.StandardCandidates);

        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "404" && e.Recommendation == "StatusCodes.Status404NotFound");
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "500" && e.Recommendation == "StatusCodes.Status500InternalServerError");
    }

    [Fact]
    public async Task Classify_HttpStatusCode_ArithmeticExpressionWithoutComparison_IsNotReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public int M(int status) => status + 404;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.StandardCandidates);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_HttpStatusCode_StatusPropertyInitializer_IsReported()
    {
        const string source = @"
namespace Test;
public sealed class Foo
{
    public int StatusCode { get; } = 404;
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source), category: MagicValueCategory.StandardCandidates);

        var entry = Assert.Single(result.Payload!.MagicValues);
        Assert.Equal("StatusCodes.Status404NotFound", entry.Recommendation);
    }

    [Fact]
    public async Task Classify_WellKnownNumbers_TimeConstantsWithoutContext_AreNotReported()
    {
        const string source = @"
using System;
namespace Test;
public sealed class Foo
{
    public const int MaxLineCount = 60;
    public void M()
    {
        var span = TimeSpan.FromMinutes(60);
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_WellKnownNumbers_BufferAndTimeoutContext_AreReported()
    {
        const string source = @"
using System.Threading;
namespace Test;
public sealed class Foo
{
    public const int ChunkSize = 4096;
    public const int BufferLength = 1024;
    public void M()
    {
        Thread.Sleep(1000);
    }
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("Foo.cs", source));

        Assert.Equal(3, result.Payload!.MagicValues.Count);
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "1000" && e.Category == "config_candidates");
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "1024" && e.Category == "standard_candidates");
        Assert.Contains(result.Payload!.MagicValues, e => e.Value == "4096" && e.Category == "standard_candidates");
    }

    [Fact]
    public async Task Classify_HolderAwareness_StaticHolderClass_DoesNotReportThresholdsOrDuplicates()
    {
        var holderSource = @"
namespace Test.Holder;
internal static class Defaults
{
    public const int MaxTimeoutSeconds = 300;
    public static readonly double Threshold = 0.65;
    public const int MaxProjects = 4;
}";
        var otherSource = @"
namespace Test.Other;
public sealed class Service
{
    public const int MaxProjects = 4;
}";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("Defaults.cs", holderSource),
            ("Service.cs", otherSource));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution);

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public async Task Classify_DuplicateConst_SameFieldNameAndValueAcrossFiles_GroupedAsDuplicates()
    {
        var source1 = @"
namespace Test.One;
public sealed class ServiceA
{
    private const int MaxRetries = 3;
    private const int RetriesMax = 3;
}";
        var source2 = @"
namespace Test.Two;
public sealed class ServiceB
{
    private const int MaxRetries = 3;
    private const int NgramSize = 3;
}";
        using var testSolution = FindMagicValuesTestHelpers.CreateSolution(
            ("ServiceA.cs", source1),
            ("ServiceB.cs", source2));

        var result = await FindMagicValuesTestHelpers.RunAsync(testSolution.Solution, category: MagicValueCategory.ConstantCandidates);

        // MaxRetries matcht in beiden Dateien -> 2 Funde. RetriesMax vs NgramSize matcht nicht -> keine Funde.
        Assert.Equal(2, result.Payload!.MagicValues.Count);
        Assert.All(result.Payload!.MagicValues, e =>
        {
            Assert.Contains("MaxRetries", e.ContextHint, StringComparison.Ordinal);
            Assert.Contains("Hochstufung", e.Recommendation, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task RegressionMarker_ProjectRegistryDefaults_ReportsZeroMagicValues()
    {
        const string source = @"
using System;
namespace Test;
internal static class ProjectRegistryDefaults
{
    public const int MaxProjects = 4;
    public static readonly TimeSpan IdleTtl = TimeSpan.FromMinutes(45);
    public static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);
}";
        var result = await FindMagicValuesTestHelpers.RunAsync(("ProjectRegistryDefaults.cs", source));

        Assert.Empty(result.Payload!.MagicValues);
    }

    [Fact]
    public void ToolDescription_DoesNotContainNoOpPhrases()
    {
        var field = typeof(AnalysisToolRegistrations).GetField("FindMagicValuesDescription",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.NotNull(field);
        var description = Assert.IsType<string>(field!.GetValue(null));

        Assert.DoesNotContain("No-op", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ainetlinter-disable MagicValues", description, StringComparison.Ordinal);
        Assert.Contains("Git-Diff", description, StringComparison.Ordinal);
    }
}
