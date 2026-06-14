#nullable enable

using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Threading.Tasks;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MaxConstructorDependenciesTests
{
    private static LinterConfig CreateConfig(int maxDeps, string[]? ignorePrefixes = null)
    {
        return new LinterConfig
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
                EnforceExplicitStateImmutability = false,
                EnforceStrictBoundaryForBusinessLogic = false,
                PreventContextDependentOverloads = false,
                RequireExplicitTruncationHandling = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxConstructorDependencies = maxDeps,
                ConstructorDependencyIgnoreTypePrefixes = ignorePrefixes ?? System.Array.Empty<string>()
            }
        };
    }

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);

        var projectInfo = ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "TestProject",
            "TestProject",
            LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    [Fact]
    public async Task Analyze_PrimaryConstructor_WithFrameworkTypesIgnored_DoesNotReportViolation()
    {
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public interface ILogger<T> {}
public interface IOptions<T> {}

public sealed class MyHandler(
    ILogger<MyHandler> logger,
    IOptions<ServiceA> options,
    ServiceA a,
    ServiceB b,
    ServiceC c)
{ }
";
        var config = CreateConfig(3, new[] { "ILogger", "IOptions" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_PrimaryConstructor_ExceedingLimitAfterFiltering_ReportsViolation()
    {
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public class ServiceD {}
public interface ILogger<T> {}
public interface IOptions<T> {}

public sealed class MyHandler(
    ILogger<MyHandler> logger, // ignored
    IOptions<ServiceA> options, // ignored
    ServiceA a,  // 1
    ServiceB b,  // 2
    ServiceC c,  // 3
    ServiceD d)  // 4 -> exceeds 3
{ }
";
        var config = CreateConfig(3, new[] { "ILogger", "IOptions" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ctorViolations = violations.Where(v => v.RuleName == "MaxConstructorDependencies").ToList();
        Assert.Single(ctorViolations);
        Assert.Contains("Primaerkonstruktor", ctorViolations.First().Details);
        Assert.Contains("Framework-Typen nicht gezaehlt", ctorViolations.First().Details);
    }

    [Fact]
    public async Task Analyze_ClassicConstructor_WithFrameworkTypesIgnored_DoesNotReportViolation()
    {
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public interface ILogger<T> {}
public interface IOptions<T> {}

public sealed class MyHandler
{
    public MyHandler(
        ILogger<MyHandler> logger,
        IOptions<ServiceA> options,
        ServiceA a,
        ServiceB b,
        ServiceC c)
    { }
}
";
        var config = CreateConfig(3, new[] { "ILogger", "IOptions" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_ClassicConstructor_ExceedingLimitAfterFiltering_ReportsViolation()
    {
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public class ServiceD {}
public interface ILogger<T> {}
public interface IOptions<T> {}

public sealed class MyHandler
{
    public MyHandler(
        ILogger<MyHandler> logger,
        IOptions<ServiceA> options,
        ServiceA a,
        ServiceB b,
        ServiceC c,
        ServiceD d)
    { }
}
";
        var config = CreateConfig(3, new[] { "ILogger", "IOptions" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ctorViolations = violations.Where(v => v.RuleName == "MaxConstructorDependencies").ToList();
        Assert.Single(ctorViolations);
        Assert.Contains("Der Konstruktor hat 4 Parameter", ctorViolations.First().Details);
        Assert.Contains("Framework-Typen nicht gezaehlt", ctorViolations.First().Details);
    }

    [Fact]
    public async Task Analyze_DefaultBehavior_CountsAllDependencies()
    {
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public interface ILogger<T> {}

public sealed class MyHandler(
    ILogger<MyHandler> logger, // counts
    ServiceA a,  // counts
    ServiceB b,  // counts
    ServiceC c)  // counts -> total 4 exceeds 3
{ }
";
        var config = CreateConfig(3, ignorePrefixes: null); // empty prefixes
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ctorViolations = violations.Where(v => v.RuleName == "MaxConstructorDependencies").ToList();
        Assert.Single(ctorViolations);
        // Note: Default behavior uses original message (no framework type hint if no prefixes configured)
        Assert.Contains("Der Primaerkonstruktor hat 4 Parameter", ctorViolations.First().Details);
    }

    [Fact]
    public async Task Analyze_QualifiedAndNullableAndGenerics_CorrectlyIgnored()
    {
        const string sourceCode = @"
namespace Microsoft.Extensions.Logging
{
    public interface ILogger<T> {}
}
namespace Microsoft.Extensions.Options
{
    public interface IOptions<T> {}
}

namespace Test
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class ServiceA {}

    public sealed class MyHandler(
        ILogger<MyHandler>? nullableLogger, // ignored because starts with ILogger
        Microsoft.Extensions.Options.IOptions<ServiceA> qualifiedOptions, // ignored because Right name is IOptions
        ServiceA a) // counts 1
    { }
}
";
        var config = CreateConfig(1, new[] { "ILogger", "IOptions" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_SubstringMatch_DoesNotIgnore()
    {
        const string sourceCode = @"
namespace Test;
public class MyCustomLogger {} // does not start with ILogger
public class ServiceA {}
public class ServiceB {}

public sealed class MyHandler(
    MyCustomLogger customLogger, // counts 1
    ServiceA a, // counts 2
    ServiceB b) // counts 3 -> exceeds 2
{ }
";
        var config = CreateConfig(2, new[] { "ILogger" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ctorViolations = violations.Where(v => v.RuleName == "MaxConstructorDependencies").ToList();
        Assert.Single(ctorViolations);
    }
}
