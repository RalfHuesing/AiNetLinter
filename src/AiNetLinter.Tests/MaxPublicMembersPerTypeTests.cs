using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MaxPublicMembersPerTypeTests
{
    private static LinterConfig CreateConfig(int limit = 5, string[]? exemptSuffixes = null) =>
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
                EnforceNoSilentCatch = false,                EnforceNoMagicValues = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxPublicMembersPerType = limit,
                MaxPublicMembersPerTypeExemptSuffixes = exemptSuffixes ?? ["Extensions", "Mapper", "Constants"]
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
    public void ClassWithMembersAtLimit_NoViolation()
    {
        const string source = @"
public sealed class OrderService {
    public void Create() {}
    public void Update() {}
    public void Cancel() {}
    public void GetById() {}
    public void GetAll() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 5));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType)));
    }

    [Fact]
    public void ClassExceedingLimit_ReturnsViolation()
    {
        const string source = @"
public sealed class OrderService {
    public void Create() {}
    public void Update() {}
    public void Cancel() {}
    public void GetById() {}
    public void GetAll() {}
    public void Delete() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 5));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType));
    }

    [Fact]
    public void PrivateMethodsNotCounted_NoViolation()
    {
        const string source = @"
public sealed class OrderService {
    public void Create() {}
    public void Update() {}
    private void Helper1() {}
    private void Helper2() {}
    private void Helper3() {}
    private void Helper4() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 2));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType)));
    }

    [Fact]
    public void ExemptSuffix_Extensions_NoViolation()
    {
        const string source = @"
public static class StringExtensions {
    public static string A(this string s) => s;
    public static string B(this string s) => s;
    public static string C(this string s) => s;
    public static string D(this string s) => s;
    public static string E(this string s) => s;
    public static string F(this string s) => s;
}";
        var violations = Analyze(source, CreateConfig(limit: 3, exemptSuffixes: ["Extensions"]));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType)));
    }

    [Fact]
    public void Limit0_Disabled_NoViolation()
    {
        const string source = @"
public sealed class BigService {
    public void A() {}
    public void B() {}
    public void C() {}
    public void D() {}
    public void E() {}
    public void F() {}
    public void G() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 0));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType)));
    }

    [Fact]
    public void PublicProperties_AreCounted()
    {
        const string source = @"
public sealed class Config {
    public string A { get; set; } = """";
    public string B { get; set; } = """";
    public string C { get; set; } = """";
    public string D { get; set; } = """";
}";
        var violations = Analyze(source, CreateConfig(limit: 3));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType));
    }

    [Fact]
    public void OverrideMethods_NotCounted()
    {
        const string source = @"
public class Base {
    public virtual void A() {}
    public virtual void B() {}
    public virtual void C() {}
}
public class Derived : Base {
    public override void A() {}
    public override void B() {}
    public override void C() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 3));
        Assert.Empty(violations.Where(v =>
            v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType) &&
            (v.Details?.Contains("Derived") ?? false)));
    }

    [Fact]
    public void Record_ExceedingLimit_ReturnsViolation()
    {
        const string source = @"
public sealed record OrderDto {
    public string Name { get; init; } = """";
    public string Address { get; init; } = """";
    public decimal Total { get; init; }
    public string Status { get; init; } = """";
}";
        var violations = Analyze(source, CreateConfig(limit: 3));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType));
    }

    [Fact]
    public void ViolationMessage_ContainsTypeName()
    {
        const string source = @"
public sealed class HugeService {
    public void A() {}
    public void B() {}
    public void C() {}
    public void D() {}
}";
        var violations = Analyze(source, CreateConfig(limit: 3));
        Assert.Contains(violations, v =>
            v.RuleName == nameof(MetricsConfig.MaxPublicMembersPerType) &&
            v.Details!.Contains("HugeService"));
    }
}
