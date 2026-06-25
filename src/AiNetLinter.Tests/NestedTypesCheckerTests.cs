using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class NestedTypesCheckerTests
{
    private static Config CreateConfig(
        bool banPublicNestedTypes = true,
        bool banPublicNestedTypesAllowPrivate = true) =>
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
                EnforceNoSilentCatch = false,                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                EnableTestSentinel = false,
                BanPublicNestedTypes = banPublicNestedTypes,
                BanPublicNestedTypesAllowPrivate = banPublicNestedTypesAllowPrivate
            },
            Metrics = new MetricsConfig()
        };

    private static IReadOnlyCollection<RuleViolation> Analyze(string source, Config config)
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

    private static IReadOnlyCollection<RuleViolation> NestedViolations(IReadOnlyCollection<RuleViolation> all) =>
        all.Where(v => v.RuleName == nameof(GlobalConfig.BanPublicNestedTypes)).ToList();

    [Fact]
    public void PublicNestedClass_ReportsViolation()
    {
        const string source = @"
public sealed class Outer {
    public class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void InternalNestedClass_ReportsViolation()
    {
        const string source = @"
public sealed class Outer {
    internal class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void PrivateNestedClass_AllowedByDefault()
    {
        const string source = @"
public sealed class Outer {
    private class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Empty(NestedViolations(violations));
    }

    [Fact]
    public void PrivateNestedClass_ReportedWhenAllowPrivateFalse()
    {
        const string source = @"
public sealed class Outer {
    private class Inner {}
}";
        var violations = Analyze(source, CreateConfig(banPublicNestedTypesAllowPrivate: false));
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void PublicNestedEnum_ReportsViolation()
    {
        const string source = @"
public sealed class Outer {
    public enum Mode { Fast, Reliable }
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Mode"));
    }

    [Fact]
    public void PublicNestedRecord_ReportsViolation()
    {
        const string source = @"
public sealed class Outer {
    public record Inner(int X);
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void PublicNestedStruct_ReportsViolation()
    {
        const string source = @"
public sealed class Outer {
    public struct Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void MultipleNestedTypes_AllReported()
    {
        const string source = @"
public sealed class Outer {
    public class A {}
    public class B {}
    public class C {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Equal(3, NestedViolations(violations).Count);
    }

    [Fact]
    public void NestedInRecord_ReportsViolation()
    {
        const string source = @"
public sealed record Outer {
    public class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void NestedInStruct_ReportsViolation()
    {
        const string source = @"
public struct Outer {
    public class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Outer.Inner"));
    }

    [Fact]
    public void Disabled_NoViolation()
    {
        const string source = @"
public sealed class Outer {
    public class Inner {}
}";
        var violations = Analyze(source, CreateConfig(banPublicNestedTypes: false));
        Assert.Empty(NestedViolations(violations));
    }

    [Fact]
    public void ViolationMessage_ContainsOuterAndInnerName()
    {
        const string source = @"
public sealed class PaymentProcessor {
    public enum PaymentStatus { Pending }
}";
        var violations = Analyze(source, CreateConfig());
        var nested = NestedViolations(violations).FirstOrDefault();
        Assert.NotNull(nested);
        Assert.Contains("PaymentProcessor.PaymentStatus", nested!.Details);
        Assert.Contains("public", nested.Details);
    }

    [Fact]
    public void ViolationPointsToNestedType_NotToOuter()
    {
        const string source = @"
public sealed class Outer {
    public class Inner {}
}";
        var violations = Analyze(source, CreateConfig());
        var nested = NestedViolations(violations).FirstOrDefault();
        Assert.NotNull(nested);
        Assert.Equal(3, nested!.LineNumber);
    }

    [Fact]
    public void MixedAccessibilities_OnlyPublicAndInternalReported()
    {
        const string source = @"
public sealed class Outer {
    public class Pub {}
    internal class Int {}
    private class Priv {}
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Equal(2, NestedViolations(violations).Count);
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Pub"));
        Assert.Contains(NestedViolations(violations), v => v.Details!.Contains("Int"));
        Assert.DoesNotContain(NestedViolations(violations), v => v.Details!.Contains("Priv"));
    }

    [Fact]
    public void NestedDelegate_NotReported()
    {
        const string source = @"
public sealed class Outer {
    public delegate void Handler(int x);
}";
        var violations = Analyze(source, CreateConfig());
        Assert.Empty(NestedViolations(violations));
    }
}
