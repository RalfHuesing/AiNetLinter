#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Baseline;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using AiNetLinter.Cache;

namespace AiNetLinter.Tests.Core;

public sealed class LinterEngineCacheTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _exeDir;

    public LinterEngineCacheTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ainetlinter-enginecache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }

            var cacheDir = Path.Combine(_exeDir, "cache");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, true);
            }
        }
        catch (Exception ignored)
        {
            System.Diagnostics.Debug.WriteLine($"Cleanup failed: {ignored.Message}");
        }
    }

    private static LinterConfig CreateDefaultConfig()
    {
        return new LinterConfig
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = true,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false
            },
            Metrics = new MetricsConfig
            {
                MaxLineCount = 500,
                MaxMethodParameterCount = 4,
                MaxCyclomaticComplexity = 5,
                MaxCognitiveComplexity = 5,
                MaxInheritanceDepth = 2
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
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var solution = workspace.CurrentSolution.AddProject(projectInfo);
        foreach (var file in files)
        {
            var documentId = DocumentId.CreateNewId(projectId);
            solution = solution.AddDocument(documentId, file.fileName, file.content);
        }
        return solution;
    }

    private async Task<Solution> CreateSolutionWithFileOnDiskAsync(string fileName, string content)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        await File.WriteAllTextAsync(filePath, content);

        var solution = CreateAdhocSolution((fileName, content));
        var documentId = solution.Projects.First().Documents.First(d => d.Name == fileName).Id;
        return solution.WithDocumentFilePath(documentId, filePath);
    }

    [Fact]
    public async Task Engine_UsesCacheOnSecondRun_AndBypassesRoslyn()
    {
        // 1. Create a class that violates EnforceSealedClasses (unsealed class)
        const string source = @"
namespace TestNamespace;
public class MyUnsealedClass
{
    public void Run() {}
}";
        var fileName = "MyUnsealedClass.cs";
        var solution = await CreateSolutionWithFileOnDiskAsync(fileName, source);
        var config = CreateDefaultConfig();
        // _tempDir contains a unique GUID → unique cache-file name per test run.
        // Without this, stale cache from a failed Dispose causes flakiness on re-runs.
        var rulesJson = $"{{ \"Global\": {{ \"EnforceSealedClasses\": true }}, \"_testRun\": \"{_tempDir.Replace("\\", "\\\\")}\" }}";

        // 2. Run engine first time — cache miss → fresh analysis
        var engine = new LinterEngine(config, rulesJson);
        var violations1 = await engine.RunAsync(solution);

        Assert.Single(violations1);
        Assert.Equal("EnforceSealedClasses", violations1.First().RuleName);

        // 3. Inject a fake violation directly via AnalysisCacheManager (avoids fragile file enumeration).
        //    The engine uses solutionPath = solution.Workspace.GetType().Name = "AdhocWorkspace"
        //    since solution.FilePath is null for in-memory solutions.
        var filePath = Path.Combine(_tempDir, fileName);
        var checksum = FileChecksumCalculator.ComputeSha256Hex(filePath);
        var cacheManager = AnalysisCacheManager.Load(_exeDir, "AdhocWorkspace", rulesJson, TimeSpan.FromMinutes(60));
        var fakeEntry = new AnalysisCacheEntry
        {
            RelativePath = fileName,
            Checksum = checksum,
            Violations = new[]
            {
                new RuleViolationDto("fake_path.cs", 999, "FakeCacheRule", "Fake details", "Fake guidance")
            }
        };
        cacheManager.Set(fileName, fakeEntry);
        cacheManager.SaveIfDirty();

        // 4. Run engine second time → should hit cache and return fake violation
        var violations2 = await engine.RunAsync(solution);
        Assert.Single(violations2);
        Assert.Equal("FakeCacheRule", violations2.First().RuleName);
        Assert.Equal(999, violations2.First().LineNumber);

        // 5. Run engine with noCache = true → should ignore cache and return the real violation
        var violations3 = await engine.RunAsync(solution, noCache: true);
        Assert.Single(violations3);
        Assert.Equal("EnforceSealedClasses", violations3.First().RuleName);
    }

    [Fact]
    public async Task Engine_DoesNotSaveCache_WhenWorkspaceHasLoadingErrors()
    {
        // 1. Create a class that violates EnforceSealedClasses (unsealed class)
        const string source = @"
namespace TestNamespace;
public class MyUnsealedClass
{
    public void Run() {}
}";
        var fileName = "MyUnsealedClass.cs";
        var solution = await CreateSolutionWithFileOnDiskAsync(fileName, source);
        var config = CreateDefaultConfig();
        var rulesJson = $"{{\"Global\": {{\"EnforceSealedClasses\": true}}, \"_testRun\": \"{_tempDir.Replace("\\", "\\\\")}\"}}";

        // 2. Wrap solution in a catalog that has loading errors
        var catalog = new SourceFileCatalog(solution, hasLoadingErrors: true);

        // 3. Run engine - because it has loading errors, cache should not be saved
        var engine = new LinterEngine(config, rulesJson);
        var violations = await engine.RunAsync(catalog);

        Assert.Single(violations);

        // 4. Verify that cache file was NOT created or doesn't exist
        var solutionPath = solution.FilePath ?? solution.Workspace.GetType().Name;
        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        var hashInput = solutionPath.ToLowerInvariant() + rulesJson;
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput));
        var hash8 = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();
        var cacheFileName = $"{solutionName}-{hash8}.json";
        var expectedCacheFilePath = Path.Combine(_exeDir, "cache", cacheFileName);

        Assert.False(File.Exists(expectedCacheFilePath));
    }
}
