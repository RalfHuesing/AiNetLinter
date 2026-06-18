using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using System.Linq;
using System.Threading.Tasks;

namespace AiNetLinter.Tests;

public sealed class MaxPartialClassFilesTests
{
    private static LinterConfig CreateConfig(int limit = 2, string[]? exemptTypes = null) =>
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
                MaxPartialClassFiles = limit,
                MaxPartialClassFilesExemptTypes = exemptTypes ?? []
            }
        };

    private static Solution CreateAdhocSolution(params (string fileName, string content)[] files)
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var projectInfo = ProjectInfo.Create(
            projectId, VersionStamp.Create(), "TestProject", "TestProject", LanguageNames.CSharp)
            .WithMetadataReferences(new[] { mscorlib })
            .WithCompilationOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    [Fact]
    public async Task PartialClass_InTwoFiles_AtLimit_NoViolation()
    {
        var solution = CreateAdhocSolution(
            ("Order.cs", "namespace App; public partial class Order { public void Execute() {} }"),
            ("Order.g.cs", "namespace App; public partial class Order { public string Name { get; set; } = \"\"; }"));
        var config = CreateConfig(limit: 2);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles)));
    }

    [Fact]
    public async Task PartialClass_InThreeFiles_ExceedsLimit2_ReturnsViolation()
    {
        var solution = CreateAdhocSolution(
            ("Order.cs", "namespace App; public partial class Order { public void Execute() {} }"),
            ("Order.Validation.cs", "namespace App; public partial class Order { public bool Validate() { return true; } }"),
            ("Order.Events.cs", "namespace App; public partial class Order { public void Raise() {} }"));
        var config = CreateConfig(limit: 2);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles));
    }

    [Fact]
    public async Task NonPartialClasses_NotAffected()
    {
        var solution = CreateAdhocSolution(
            ("Service1.cs", "namespace App; public sealed class ServiceA { public void A() {} }"),
            ("Service2.cs", "namespace App; public sealed class ServiceB { public void B() {} }"));
        var config = CreateConfig(limit: 2);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles)));
    }

    [Fact]
    public async Task ExemptType_BySimpleName_NoViolation()
    {
        var solution = CreateAdhocSolution(
            ("LegacyClass.cs", "namespace App; public partial class LegacyClass { public void A() {} }"),
            ("LegacyClass.Part2.cs", "namespace App; public partial class LegacyClass { public void B() {} }"),
            ("LegacyClass.Part3.cs", "namespace App; public partial class LegacyClass { public void C() {} }"));
        var config = CreateConfig(limit: 2, exemptTypes: ["LegacyClass"]);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles)));
    }

    [Fact]
    public async Task Limit0_Disabled_NoViolation()
    {
        var solution = CreateAdhocSolution(
            ("Order.cs", "namespace App; public partial class Order { public void A() {} }"),
            ("Order.B.cs", "namespace App; public partial class Order { public void B() {} }"),
            ("Order.C.cs", "namespace App; public partial class Order { public void C() {} }"),
            ("Order.D.cs", "namespace App; public partial class Order { public void D() {} }"));
        var config = CreateConfig(limit: 0);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles)));
    }

    [Fact]
    public async Task ViolationMessage_ContainsTypeName()
    {
        var solution = CreateAdhocSolution(
            ("Widget.cs", "namespace App; public partial class Widget { public void A() {} }"),
            ("Widget.Part2.cs", "namespace App; public partial class Widget { public void B() {} }"),
            ("Widget.Part3.cs", "namespace App; public partial class Widget { public void C() {} }"));
        var config = CreateConfig(limit: 2);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Contains(violations, v =>
            v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles) &&
            v.Details!.Contains("Widget"));
    }

    [Fact]
    public async Task TwoDistinctPartialTypes_EachInTwoFiles_NoViolation()
    {
        var solution = CreateAdhocSolution(
            ("Alpha.cs", "namespace App; public partial class Alpha { public void A() {} }"),
            ("Alpha.g.cs", "namespace App; public partial class Alpha { public void B() {} }"),
            ("Beta.cs", "namespace App; public partial class Beta { public void X() {} }"),
            ("Beta.g.cs", "namespace App; public partial class Beta { public void Y() {} }"));
        var config = CreateConfig(limit: 2);
        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(solution);
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxPartialClassFiles)));
    }
}
