using Xunit;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter.Tests;

public sealed class MaxDirectoryChildrenTests : IDisposable
{
    private readonly string _tempDir;

    public MaxDirectoryChildrenTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiNetLinterDirTest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static LinterConfig CreateConfig(int limit, string[]? exemptNames = null) =>
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
                EnforceNoSilentCatch = false,
                EnforceNoVariableShadowing = false,
                EnforceReadonlyParameters = false,
                EnforceReadonlyFields = false,
                EnforceNoMagicValues = false,
                EnforceExplicitStateImmutability = false,
                EnforceStrictBoundaryForBusinessLogic = false,
                PreventContextDependentOverloads = false,
                RequireExplicitTruncationHandling = false,
                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false
            },
            Metrics = new MetricsConfig
            {
                MaxDirectoryChildren = limit,
                MaxDirectoryChildrenExemptNames = exemptNames ?? ["Migrations", "Generated", "wwwroot", "obj", "bin", ".git"]
            },
            SolutionBasePath = null
        };

    private static RuleViolation[] RunCheck(string dir, LinterConfig config)
    {
        var violations = new ConcurrentBag<RuleViolation>();
        var configWithBase = config with { SolutionBasePath = dir };
        PostAnalysisChecks.RunMaxDirectoryChildrenCheck(violations, configWithBase);
        return violations.ToArray();
    }

    private static void CreateFiles(string dir, int count, string prefix = "File")
    {
        for (var i = 0; i < count; i++)
            File.WriteAllText(Path.Combine(dir, $"{prefix}{i}.cs"), "");
    }

    [Fact]
    public void DirectoryAtLimit_NoViolation()
    {
        CreateFiles(_tempDir, 12);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren)));
    }

    [Fact]
    public void DirectoryExceedingLimit_ReturnsViolation()
    {
        CreateFiles(_tempDir, 13);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren));
    }

    [Fact]
    public void SubdirectoriesCountAsEntries()
    {
        CreateFiles(_tempDir, 6);
        for (var i = 0; i < 7; i++)
            Directory.CreateDirectory(Path.Combine(_tempDir, $"Sub{i}"));
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Contains(violations, v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren));
    }

    [Fact]
    public void ExemptDirectoryName_NotChecked()
    {
        var migrationsDir = Path.Combine(_tempDir, "Migrations");
        Directory.CreateDirectory(migrationsDir);
        CreateFiles(migrationsDir, 50);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12, exemptNames: ["Migrations"]));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren)));
    }

    [Fact]
    public void ExemptDirectory_CaseInsensitive()
    {
        var dir = Path.Combine(_tempDir, "migrations");
        Directory.CreateDirectory(dir);
        CreateFiles(dir, 50);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12, exemptNames: ["Migrations"]));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren)));
    }

    [Fact]
    public void Limit0_Disabled_NoViolation()
    {
        CreateFiles(_tempDir, 100);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 0));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren)));
    }

    [Fact]
    public void RecursiveCheck_NestedDirectoryExceedingLimit_ReturnsViolation()
    {
        var subDir = Path.Combine(_tempDir, "Models");
        Directory.CreateDirectory(subDir);
        CreateFiles(subDir, 20);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Contains(violations, v =>
            v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren) &&
            v.FilePath.Contains("Models"));
    }

    [Fact]
    public void ViolationDetails_ContainEntryCount()
    {
        CreateFiles(_tempDir, 15);
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Contains(violations, v =>
            v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren) &&
            v.Details!.Contains("15"));
    }

    [Fact]
    public void EmptyDirectory_NoViolation()
    {
        var violations = RunCheck(_tempDir, CreateConfig(limit: 12));
        Assert.Empty(violations.Where(v => v.RuleName == nameof(MetricsConfig.MaxDirectoryChildren)));
    }
}
