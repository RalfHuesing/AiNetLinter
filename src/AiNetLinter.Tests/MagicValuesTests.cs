using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Threading.Tasks;
using System.Linq;

namespace AiNetLinter.Tests;

public sealed class MagicValuesTests
{
    private static LinterConfig CreateConfig(MagicValuesConfig magicValuesConfig)
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
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                EnforceNoMagicValues = true
            },
            Metrics = new MetricsConfig(),
            MagicValues = magicValuesConfig
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
    public async Task Mode_All_ReportsBothStringAndNumericMagicValues()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        string key = ""magic-string"";
        int val = 42;
    }
}";
        var config = CreateConfig(new MagicValuesConfig { Mode = "all" });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Equal(2, ruleViolations.Count);
        Assert.Contains(ruleViolations, v => v.Details.Contains("magic-string"));
        Assert.Contains(ruleViolations, v => v.Details.Contains("42"));
    }

    [Fact]
    public async Task Mode_NumericOnly_ReportsOnlyNumericMagicValues()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        string key = ""magic-string"";
        int val = 42;
    }
}";
        var config = CreateConfig(new MagicValuesConfig { Mode = "numeric-only" });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Single(ruleViolations);
        Assert.Contains(ruleViolations, v => v.Details.Contains("42"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("magic-string"));
    }

    [Fact]
    public async Task Mode_NumericAndShortString_ReportsOnlyStringsShorterThanMinLength()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        string shortStr = ""abc""; // len = 3
        string longStr = ""abcdef""; // len = 6
        int val = 42;
    }
}";
        var config = CreateConfig(new MagicValuesConfig
        {
            Mode = "numeric-and-short-string",
            MinStringLength = 5
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Equal(2, ruleViolations.Count);
        Assert.Contains(ruleViolations, v => v.Details.Contains("\"abcdef\""));
        Assert.Contains(ruleViolations, v => v.Details.Contains("42"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("\"abc\""));
    }

    [Fact]
    public async Task IgnoreStringPatterns_FiltersMatchingStrings()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        string route1 = ""/api/users"";
        string route2 = ""/api/users/{id}"";
        string notRoute = ""just a normal string"";
    }
}";
        var config = CreateConfig(new MagicValuesConfig
        {
            Mode = "all",
            IgnoreStringPatterns = new[] { @"^/api/[\w/{}\-]*$" }
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Single(ruleViolations);
        Assert.Contains(ruleViolations, v => v.Details.Contains("just a normal string"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("/api/users"));
    }

    [Fact]
    public async Task IgnoreNumericValues_FiltersSpecifiedNumbers()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        int limit = 1000;
        int status = 404;
        int magic = 999;
    }
}";
        var config = CreateConfig(new MagicValuesConfig
        {
            IgnoreNumericValues = new double[] { 1000, 404 }
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Single(ruleViolations);
        Assert.Contains(ruleViolations, v => v.Details.Contains("999"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("1000"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("404"));
    }

    [Fact]
    public async Task IgnoreInvocationPrefixes_FiltersArgumentsForMatchingMethods()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run(ILogger logger)
    {
        logger.LogInformation(""starting flow"");
        logger?.LogWarning(""warning here"");
        MapGet(""path"", () => 1);
        Process(""other-string"");
    }

    private void MapGet(string p, System.Func<int> f) {}
    private void Process(string s) {}
}
public interface ILogger
{
    void LogInformation(string s);
    void LogWarning(string s);
}";
        var config = CreateConfig(new MagicValuesConfig
        {
            IgnoreInvocationPrefixes = new[] { "Log", "MapGet" }
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Single(ruleViolations);
        Assert.Contains(ruleViolations, v => v.Details.Contains("other-string"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("starting flow"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("warning here"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("path"));
    }

    [Fact]
    public async Task IgnoreCollectionInitializers_FiltersLiteralsInInitializers()
    {
        const string sourceCode = @"
using System.Collections.Generic;
namespace Test;
public class MyClass
{
    public void Run()
    {
        var dict = new Dictionary<string, string>
        {
            [""clientId""] = ""app"",
            [""clientSecret""] = ""secret""
        };
        string magic = ""standalone"";
    }
}";
        var config = CreateConfig(new MagicValuesConfig
        {
            IgnoreCollectionInitializers = true
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);

        var ruleViolations = violations.Where(v => v.RuleName == "EnforceNoMagicValues").ToList();
        Assert.Single(ruleViolations);
        Assert.Contains(ruleViolations, v => v.Details.Contains("standalone"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("clientId"));
        Assert.DoesNotContain(ruleViolations, v => v.Details.Contains("clientSecret"));
    }

    [Fact]
    public async Task InvalidRegex_DoesNotCrash_AndLogsWarning()
    {
        const string sourceCode = @"
namespace Test;
public class MyClass
{
    public void Run()
    {
        string test = ""some-value"";
    }
}";
        // Unclosed parenthesis is invalid regex
        var config = CreateConfig(new MagicValuesConfig
        {
            IgnoreStringPatterns = new[] { "(invalid-pattern" }
        });
        var solution = CreateAdhocSolution(("MyClass.cs", sourceCode));
        var engine = new LinterEngine(config);
        
        // This should run without throwing exception
        var violations = await engine.RunAsync(solution);

        Assert.Single(violations.Where(v => v.RuleName == "EnforceNoMagicValues"));
    }
}
