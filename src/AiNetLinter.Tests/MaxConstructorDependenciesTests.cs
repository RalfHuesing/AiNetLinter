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
    private static Config CreateConfig(int maxDeps, string[]? ignorePrefixes = null, string[]? exemptSuffixes = null)
    {
        return new Config
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
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxConstructorDependencies = maxDeps,
                ConstructorDependencyIgnoreTypePrefixes = ignorePrefixes ?? System.Array.Empty<string>(),
                ConstructorDependencyExemptClassSuffixes = exemptSuffixes ?? System.Array.Empty<string>()
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

    // --- Exception-Klassen-Exemption ---

    [Fact]
    public async Task Analyze_ExceptionClass_PrimaryConstructor_DoesNotReportViolation()
    {
        // Exception-Klassen haben Payload-Parameter (string, Exception?), keine DI-Abhängigkeiten.
        // ConstructorDependencyExemptClassSuffixes: ["Exception"] verhindert False Positives.
        const string sourceCode = @"
namespace Test;
public sealed class SiteMetadataValidationException(
    string code,
    string message,
    string? componentId = null,
    string? pageRoute = null,
    string? suggestion = null,
    System.Exception? innerException = null) : System.Exception(message, innerException)
{ }
";
        var config = CreateConfig(5, exemptSuffixes: new[] { "Exception" });
        var solution = CreateAdhocSolution(("SiteMetadataValidationException.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_ExceptionClass_ClassicConstructor_DoesNotReportViolation()
    {
        const string sourceCode = @"
namespace Test;
public sealed class DomainException : System.Exception
{
    public DomainException(
        string code,
        string message,
        string? componentId = null,
        string? pageRoute = null,
        string? suggestion = null,
        System.Exception? innerException = null) : base(message, innerException)
    { }
}
";
        var config = CreateConfig(5, exemptSuffixes: new[] { "Exception" });
        var solution = CreateAdhocSolution(("DomainException.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_NonExemptClass_ExceedingLimit_ReportsViolation()
    {
        // Stellt sicher, dass die Exemption NUR für konfigurierte Suffixe greift.
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public class ServiceD {}
public class ServiceE {}
public class ServiceF {}

public sealed class MyHandler(
    ServiceA a,
    ServiceB b,
    ServiceC c,
    ServiceD d,
    ServiceE e,
    ServiceF f)
{ }
";
        var config = CreateConfig(5, exemptSuffixes: new[] { "Exception" });
        var solution = CreateAdhocSolution(("MyHandler.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Single(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    // --- Options/Config-Record Exemption ---

    [Fact]
    public async Task Analyze_OptionsRecord_AllDefaultValues_DoesNotReportViolation()
    {
        // Reproduziert den False-Positive aus Research/False-Positives/Record.md:
        // Records mit ausschließlich Default-Werten sind Options/Config-Objects, keine DI-Klassen.
        const string sourceCode = @"
namespace Test;
public sealed record RunOptions(
    bool Verbose = false,
    bool DryRun = false,
    bool OnlyChanged = false,
    bool CheckOnly = false,
    bool ReadmeOnly = false,
    string? GitSince = null,
    string? BaselinePath = null,
    string? PlaybookPath = null,
    string? GraphPath = null,
    string OutputFormat = ""text"")
{
    public static RunOptions Default { get; } = new();
}
";
        var config = CreateConfig(5);
        var solution = CreateAdhocSolution(("RunOptions.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_Record_NoDefaultValues_DoesNotReportViolation()
    {
        // Records definieren Datenfelder über ihren Primärkonstruktor — keine DI-Abhängigkeiten.
        // Ein "DI-Record" (Record als Service-Target) ist ein theoretisches Konstrukt das in der
        // Praxis nicht vorkommt; alle realen Fälle sind Daten-Records (false positive).
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public class ServiceD {}
public class ServiceE {}
public class ServiceF {}

public sealed record MyDataRecord(
    ServiceA A,
    ServiceB B,
    ServiceC C,
    ServiceD D,
    ServiceE E,
    ServiceF F);
";
        var config = CreateConfig(5);
        var solution = CreateAdhocSolution(("MyDataRecord.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_Record_MixedDefaults_DoesNotReportViolation()
    {
        // Auch Records mit gemischten Parametern (Required + Optional) sind Daten-Records.
        const string sourceCode = @"
namespace Test;
public class ServiceA {}
public class ServiceB {}
public class ServiceC {}
public class ServiceD {}
public class ServiceE {}
public class ServiceF {}

public sealed record MyDataRecord(
    ServiceA A,
    ServiceB B,
    ServiceC C,
    ServiceD D,
    ServiceE E,
    ServiceF F,
    bool IsEnabled = false);
";
        var config = CreateConfig(5);
        var solution = CreateAdhocSolution(("MyDataRecord.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_DataRecord_PositionalFields_DoesNotReportViolation()
    {
        // Reproduziert den realen False-Positive: Domain-Daten-Record mit vielen Pflichtfeldern.
        const string sourceCode = @"
namespace Test;
public sealed record WochenModell(
    int ModellId,
    int ModellIDTagMontag,
    int ModellIDTagDienstag,
    int ModellIDTagMittwoch,
    int ModellIDTagDonnerstag,
    int ModellIDTagFreitag,
    int ModellIDTagSamstag,
    int ModellIDTagSonntag);
";
        var config = CreateConfig(5);
        var solution = CreateAdhocSolution(("WochenModell.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }

    [Fact]
    public async Task Analyze_OptionsStruct_AllDefaultValues_DoesNotReportViolation()
    {
        // Structs mit ausschließlich Default-Werten sind Value/Config-Objects — analog zu Records.
        const string sourceCode = @"
namespace Test;
public readonly struct RenderOptions(
    int Width = 1920,
    int Height = 1080,
    int Dpi = 96,
    bool Antialias = true,
    bool Transparent = false,
    float Scale = 1.0f);
";
        var config = CreateConfig(5);
        var solution = CreateAdhocSolution(("RenderOptions.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        Assert.Empty(violations.Where(v => v.RuleName == "MaxConstructorDependencies"));
    }
}
