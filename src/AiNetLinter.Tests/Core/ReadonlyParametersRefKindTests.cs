using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class ReadonlyParametersRefKindTests
{
    private static LinterConfig CreateConfig(bool readonlyParams = true) => new()
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
            EnforceNoVariableShadowing = false,
            EnforceReadonlyParameters = readonlyParams,
            EnforceReadonlyFields = false,
            EnforceNoMagicValues = false,
            EnforceExplicitStateImmutability = false,            PreventContextDependentOverloads = false,            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false
        },
        Metrics = new MetricsConfig
        {
            MaxLineCount = 500,
            MaxMethodParameterCount = 10,
            MaxCyclomaticComplexity = 20,
            MaxCognitiveComplexity = 20
        }
    };

    private static SemanticModel GetSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("TestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        if (errors.Count > 0)
            throw new System.Exception("Compilation errors:\n" + string.Join("\n", errors));

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void NormalParameter_Reassignment_IsViolation()
    {
        const string source = @"
public sealed class MyService
{
    public void Process(int value)
    {
        value = 42;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig());
        Assert.Contains(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }

    [Fact]
    public void OutParameter_Reassignment_NoViolation()
    {
        const string source = @"
public sealed class MyService
{
    public void TryGet(out int value)
    {
        value = 42;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig());
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }

    [Fact]
    public void RefParameter_Reassignment_NoViolation()
    {
        const string source = @"
public sealed class MyService
{
    public void Update(ref int value)
    {
        value = 42;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig());
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }

    [Fact]
    public void InParameter_NoViolation()
    {
        const string source = @"
public sealed class MyService
{
    public int Read(in int value)
    {
        return value + 1;
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig());
        Assert.DoesNotContain(violations, v => v.RuleName == "EnforceReadonlyParameters");
    }
}
