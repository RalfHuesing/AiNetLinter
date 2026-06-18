using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MaxBoolParameterCountTests
{
    private static LinterConfig CreateConfig(int limit = 1, bool allowPrivate = true, string[]? exemptPrefixes = null) =>
        new()
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
                EnforceNoVariableShadowing = false,
                EnforceReadonlyParameters = false,
                EnforceReadonlyFields = false,
                EnforceNoMagicValues = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxBoolParameterCount = limit,
                MaxBoolParameterCountAllowPrivate = allowPrivate,
                MaxBoolParameterCountExemptMethodPrefixes = exemptPrefixes ?? []
            }
        };

    private static IReadOnlyCollection<RuleViolation> Analyze(string source, LinterConfig config)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var semanticModel = compilation.GetSemanticModel(tree);
        return LinterAnalyzer.Analyze("Test.cs", semanticModel, config);
    }

    [Fact]
    public void PublicMethod_WithOneBoolParam_AtLimit_NoViolation()
    {
        const string source = @"
public sealed class Service {
    public void Configure(bool enableCaching) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void PublicMethod_WithTwoBoolParams_ExceedsLimit1_ReturnsViolation()
    {
        const string source = @"
public sealed class Service {
    public void Configure(bool enableCaching, bool enableLogging) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }

    [Fact]
    public void PrivateMethod_WithTwoBoolParams_AllowPrivateTrue_NoViolation()
    {
        const string source = @"
public sealed class Service {
    private void Internal(bool fast, bool verbose) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1, allowPrivate: true));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void PrivateMethod_WithTwoBoolParams_AllowPrivateFalse_ReturnsViolation()
    {
        const string source = @"
public sealed class Service {
    private void Internal(bool fast, bool verbose) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1, allowPrivate: false));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }

    [Fact]
    public void Method_MatchingExemptPrefix_NoViolation()
    {
        const string source = @"
public sealed class Service {
    public bool TryParse(string input, bool strict, bool throwOnError) { return false; }
}";
        var violations = Analyze(source, CreateConfig(limit: 1, exemptPrefixes: ["Try"]));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void Method_NoBoolParams_NoViolation()
    {
        const string source = @"
public sealed class Service {
    public void Configure(string name, int timeout) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void Method_NullableBool_CountsAsBool()
    {
        const string source = @"
public sealed class Service {
    public void Configure(bool? enableCaching, bool? enableLogging) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }

    [Fact]
    public void Method_MixedBoolAndOtherParams_CountsOnlyBools()
    {
        const string source = @"
public sealed class Service {
    public void Configure(string name, bool enableCaching, bool enableLogging) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }

    [Fact]
    public void Limit0_Disabled_NoViolation()
    {
        const string source = @"
public sealed class Service {
    public void Configure(bool a, bool b, bool c) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 0));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void PublicConstructor_WithTwoBoolParams_ReturnsViolation()
    {
        const string source = @"
public sealed class Service {
    public Service(bool enableCaching, bool enableLogging) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }

    [Fact]
    public void ProtectedMethod_WithTwoBoolParams_AllowPrivateTrue_NoViolation()
    {
        const string source = @"
public class Service {
    protected void Internal(bool fast, bool verbose) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 1, allowPrivate: true));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount)));
    }

    [Fact]
    public void Limit2_ThreeBoolParams_ReturnsViolation()
    {
        const string source = @"
public sealed class Service {
    public void Send(bool includeAttachments, bool isHtml, bool requireReadReceipt) {}
}";
        var violations = Analyze(source, CreateConfig(limit: 2));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxBoolParameterCount));
    }
}
