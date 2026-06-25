#nullable enable

// @covers ComplexityChecker
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MaxSwitchArmsTests
{
    private static SemanticModel CreateSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        return compilation.GetSemanticModel(tree);
    }

    private static Config CreateConfig(
        int maxSwitchArms = 10,
        bool excludeDispatcher = true,
        string[]? exemptTypes = null)
    {
        return new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
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
                MaxSwitchArms = maxSwitchArms,
                MaxSwitchArmsExcludeDispatcher = excludeDispatcher,
                MaxSwitchArmsExemptTypes = exemptTypes ?? [],
                ExcludeSwitchDispatcherCases = true,
                SwitchDispatcherMaxCaseBodyLines = 3,
            }
        };
    }

    [Fact]
    public void SwitchExpression_WithMoreArmsThanLimit_ReportsViolation()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status)
    {
        var x = status;
        return status switch
        {
            1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
            6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
            11 => ""K"", _ => ""X""
        };
    }
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.Single(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void SwitchExpression_WithExactlyLimit_IsOk()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status) => status switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.DoesNotContain(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void SwitchStatement_LabelsOverLimit_ReportsViolation()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd)
    {
        var x = cmd;
        switch (cmd)
        {
            case 1: return 1;
            case 2: return 2;
            case 3: return 3;
            case 4: return 4;
            case 5: return 5;
            case 6: return 6;
            case 7: return 7;
            case 8: return 8;
            case 9: return 9;
            case 10: return 10;
            case 11: return 11;
            default: return 0;
        }
    }
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 10));

        Assert.Single(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void DispatcherMethod_WithManyArms_IsExempt_WhenExcludeDispatcherIsTrue()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd) => cmd switch
    {
        1 => HandleA(cmd), 2 => HandleB(cmd), 3 => HandleC(cmd),
        4 => HandleD(cmd), 5 => HandleE(cmd), 6 => HandleF(cmd),
        7 => HandleG(cmd), 8 => HandleH(cmd), 9 => HandleI(cmd),
        10 => HandleJ(cmd), 11 => HandleK(cmd), _ => 0
    };
    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
    private int HandleD(int x) => x;
    private int HandleE(int x) => x;
    private int HandleF(int x) => x;
    private int HandleG(int x) => x;
    private int HandleH(int x) => x;
    private int HandleI(int x) => x;
    private int HandleJ(int x) => x;
    private int HandleK(int x) => x;
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, excludeDispatcher: true));

        Assert.DoesNotContain(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void DispatcherMethod_WithManyArms_ReportsViolation_WhenExcludeDispatcherIsFalse()
    {
        const string source = @"
public class Router
{
    public int Route(int cmd) => cmd switch
    {
        1 => HandleA(cmd), 2 => HandleB(cmd), 3 => HandleC(cmd),
        4 => HandleD(cmd), 5 => HandleE(cmd), 6 => HandleF(cmd),
        7 => HandleG(cmd), 8 => HandleH(cmd), 9 => HandleI(cmd),
        10 => HandleJ(cmd), 11 => HandleK(cmd), _ => 0
    };
    private int HandleA(int x) => x;
    private int HandleB(int x) => x;
    private int HandleC(int x) => x;
    private int HandleD(int x) => x;
    private int HandleE(int x) => x;
    private int HandleF(int x) => x;
    private int HandleG(int x) => x;
    private int HandleH(int x) => x;
    private int HandleI(int x) => x;
    private int HandleJ(int x) => x;
    private int HandleK(int x) => x;
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, excludeDispatcher: false));

        Assert.Single(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void ExemptType_WithManyArms_IsOk()
    {
        const string source = @"
public class OrderStateMachine
{
    public string Transition(int state) => state switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
        11 => ""K"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model,
            CreateConfig(maxSwitchArms: 10, exemptTypes: ["OrderStateMachine"]));

        Assert.DoesNotContain(violations, v => v.RuleName == "MaxSwitchArms");
    }

    [Fact]
    public void MaxSwitchArmsZero_Disabled_NoViolation()
    {
        const string source = @"
public class Order
{
    public string GetLabel(int status) => status switch
    {
        1 => ""A"", 2 => ""B"", 3 => ""C"", 4 => ""D"", 5 => ""E"",
        6 => ""F"", 7 => ""G"", 8 => ""H"", 9 => ""I"", 10 => ""J"",
        11 => ""K"", 12 => ""L"", 13 => ""M"", _ => ""X""
    };
}";
        var model = CreateSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(maxSwitchArms: 0));

        Assert.DoesNotContain(violations, v => v.RuleName == "MaxSwitchArms");
    }
}
