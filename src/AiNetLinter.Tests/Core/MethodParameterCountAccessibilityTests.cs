#nullable enable
using System.Linq;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class MethodParameterCountAccessibilityTests
{
    private static LinterConfig CreateConfig(
        int maxParams = 4,
        bool allowPrivate = false,
        int forNonPublic = 0) => new()
    {
        Global = new GlobalConfig
        {
            EnforceSealedClasses = false,
            AllowDynamic = false,
            AllowOutParameters = true,
            EnforceValueObjectContracts = false,
            EnforcePascalCase = false,
            EnforceXmlDocumentation = false,
            EnforceSemanticNaming = false,
            EnforceNullableEnable = false,
            EnforceNoSilentCatch = false,
            EnforceExplicitStateImmutability = false,
            PreventContextDependentOverloads = false,
            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = maxParams,
            MaxCyclomaticComplexity = 20,
            MaxCognitiveComplexity = 20,
            MethodParameterCountIgnoreTypeNames = [],
            MaxMethodParameterCountAllowPrivate = allowPrivate,
            MaxMethodParameterCountForNonPublic = forNonPublic
        }
    };

    private static IReadOnlyCollection<RuleViolation> Analyze(string source, LinterConfig config)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return LinterAnalyzer.Analyze("Test.cs", compilation.GetSemanticModel(tree), config);
    }

    // AllowPrivate: true — vollständige Ausnahme für private/protected

    [Fact]
    public void AllowPrivate_PrivateMethod_ExceedsLimit_NoViolation()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e) {}
}";
        var violations = Analyze(source, CreateConfig(maxParams: 4, allowPrivate: true));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void AllowPrivate_ProtectedMethod_ExceedsLimit_NoViolation()
    {
        const string source = @"
public class Service {
    protected void Helper(string a, string b, string c, string d, string e) {}
}";
        var violations = Analyze(source, CreateConfig(maxParams: 4, allowPrivate: true));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void AllowPrivate_PublicMethod_ExceedsLimit_Violation()
    {
        const string source = @"
public sealed class Service {
    public void Execute(string a, string b, string c, string d, string e) {}
}";
        var violations = Analyze(source, CreateConfig(maxParams: 4, allowPrivate: true));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void AllowPrivate_False_PrivateMethod_ExceedsLimit_Violation()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e) {}
}";
        var violations = Analyze(source, CreateConfig(maxParams: 4, allowPrivate: false));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    // ForNonPublic: relaxiertes Limit für private/protected

    [Fact]
    public void ForNonPublic_PrivateMethod_WithinRelaxedLimit_NoViolation()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e) {}
}";
        // 5 params, nonPublicLimit=6 → ok
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 6));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ForNonPublic_PrivateMethod_ExceedsRelaxedLimit_Violation()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e, string f, string g) {}
}";
        // 7 params, nonPublicLimit=6 → violation
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 6));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ForNonPublic_PublicMethod_UsesStrictLimit_Violation()
    {
        const string source = @"
public sealed class Service {
    public void Execute(string a, string b, string c, string d, string e) {}
}";
        // 5 params, mainLimit=4, nonPublicLimit=6 — public → still strict limit
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 6));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ForNonPublic_ProtectedMethod_WithinRelaxedLimit_NoViolation()
    {
        const string source = @"
public class Service {
    protected void Template(string a, string b, string c, string d, string e) {}
}";
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 6));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ForNonPublic_Zero_PrivateMethod_UsesMainLimit()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e) {}
}";
        // forNonPublic=0 → main limit applies
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 0));
        Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void AllowPrivate_TakesPrecedenceOver_ForNonPublic()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e, string f, string g, string h) {}
}";
        // 8 params, nonPublicLimit=6 would still fail, but AllowPrivate skips check entirely
        var violations = Analyze(source, CreateConfig(maxParams: 4, allowPrivate: true, forNonPublic: 6));
        Assert.Empty(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
    }

    [Fact]
    public void ForNonPublic_ViolationMessage_MentionsRelaxedLimit()
    {
        const string source = @"
public sealed class Service {
    private void Helper(string a, string b, string c, string d, string e, string f, string g) {}
}";
        // 7 params, nonPublicLimit=6 → violation, message should mention limit 6
        var violations = Analyze(source, CreateConfig(maxParams: 4, forNonPublic: 6));
        var v = Assert.Single(violations.Where(v => v.RuleName == "MaxMethodParameterCount"));
        Assert.Contains("7", v.Details);
        Assert.Contains("6", v.Details);
    }
}
