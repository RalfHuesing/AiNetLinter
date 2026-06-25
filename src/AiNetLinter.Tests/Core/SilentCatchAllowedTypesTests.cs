using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter.Tests.Core;

public sealed class SilentCatchAllowedTypesTests
{
    private static Config CreateConfig(string[]? allowedTypes = null) => new()
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
            EnforceNoSilentCatch = true,
            AllowCancellationShutdownCatch = false,
            AllowedSilentCatchExceptionTypes = allowedTypes ?? [],            EnforceExplicitStateImmutability = false,            PreventContextDependentOverloads = false,            EnforceNamespaceDirectoryMapping = false,
            DetectAndBanPhantomDependencies = false,
            EnforceResultPatternOverExceptions = false
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
    public void AllowedType_EmptyCatch_NoViolation()
    {
        const string source = @"
using System;
public sealed class JsInterop
{
    public class JSDisconnectedException : Exception { }

    public void Dispose()
    {
        try { }
        catch (JSDisconnectedException) { }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(["JSDisconnectedException"]));
        Assert.Empty(violations.Where(v => v.RuleName == "EnforceNoSilentCatch"));
    }

    [Fact]
    public void NotAllowedType_EmptyCatch_Violation()
    {
        const string source = @"
using System;
public sealed class JsInterop
{
    public class SomeOtherException : Exception { }

    public void Dispose()
    {
        try { }
        catch (SomeOtherException) { }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(["JSDisconnectedException"]));
        Assert.Single(violations.Where(v => v.RuleName == "EnforceNoSilentCatch"));
    }

    [Fact]
    public void AllowedTypesEmpty_EmptyCatch_Violation()
    {
        const string source = @"
using System;
public sealed class JsInterop
{
    public class JSDisconnectedException : Exception { }

    public void Dispose()
    {
        try { }
        catch (JSDisconnectedException) { }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig([]));
        Assert.Single(violations.Where(v => v.RuleName == "EnforceNoSilentCatch"));
    }

    [Fact]
    public void AllowedType_WithVariable_Violation()
    {
        const string source = @"
using System;
public sealed class JsInterop
{
    public class JSDisconnectedException : Exception { }

    public void Dispose()
    {
        try { }
        catch (JSDisconnectedException e)
        {
            var _ = e;
        }
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("Test.cs", model, CreateConfig(["JSDisconnectedException"]));
        Assert.Single(violations.Where(v => v.RuleName == "EnforceNoSilentCatch"));
    }
}
