#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AiNetLinter.IntegrationTests.Cli;

/// <summary>
/// MSE-Baustein "ein repraesentativer CLI-Adapter mit Exit-Code": ruft
/// <see cref="AiNetLinter.Program.Main(string[])"/> in-process auf gegen zwei isolierte
/// Kopien der Mini-Fixture <c>tests/Fixtures/BaselineMini</c> -- eine mit unveraendertem
/// (unsealed) <c>ViolatingClass.cs</c> fuer den Verstoss-Fall, eine mit einer sealed-Variante fuer
/// den sauberen Fall -- und prueft den resultierenden Prozess-Exit-Code. Die kopierte
/// <c>rules.json</c> wird durch eine minimale, vollstaendig kontrollierte Konfiguration ersetzt
/// (nur EnforceSealedClasses aktiv, alle anderen Regeln bewusst deaktiviert bzw. grosszuegig),
/// damit der Exit-Code-Kontrast ausschliesslich vom sealed/unsealed-Unterschied abhaengt und nicht
/// von zufaelligen Treffern anderer, in der Original-Fixture ebenfalls aktiver Regeln.
/// </summary>
[Trait("Category", "Integration")]
public sealed class CliAdapterExitCodeTests
{
    private const string PermissiveRulesJson = """
        {
          "Global": {
            "EnforceSealedClasses": true,
            "AllowUnsealedPartialClasses": true,
            "AllowDynamic": true,
            "AllowOutParameters": true,
            "EnforceValueObjectContracts": false,
            "EnableTestSentinel": false,
            "EnforcePascalCase": false,
            "EnforceAsciiIdentifiers": false,
            "EnforceXmlDocumentation": false,
            "EnforceSemanticNaming": false,
            "EnforceNullableEnable": false,
            "EnforceNoSilentCatch": false,
            "EnforceResultPatternOverExceptions": false,
            "EnforceExplicitStateImmutability": false,
            "EnforceNamespaceDirectoryMapping": false,
            "DetectAndBanPhantomDependencies": false,
            "BanPublicNestedTypes": false,
            "AvoidExcessiveMiddleMen": false,
            "EnablePerformanceProfiling": false,
            "BanAsyncVoid": false,
            "BanBlockingTaskAccess": false,
            "EnableDuplicateCodeCheck": false
          },
          "Metrics": {
            "MaxLineCount": 5000,
            "MaxMethodParameterCount": 20,
            "MaxMethodLineCount": 5000,
            "MaxCyclomaticComplexity": 100,
            "MaxCognitiveComplexity": 100,
            "MaxInheritanceDepth": 20,
            "MaxDirectoryDepth": 20,
            "MaxDirectoryChildren": 0,
            "MaxPartialClassFiles": 0,
            "MaxPublicMembersPerType": 0,
            "MaxLinqChainLength": 0,
            "MaxSwitchArms": 0,
            "MaxBoolParameterCount": 0,
            "MaxConstructorDependencies": 20,
            "MaxMethodOverloads": 20
          }
        }
        """;

    [Fact]
    public async Task Main_AgainstFixtureWithUnsealedClass_ReturnsNonZeroExitCode()
    {
        var (rootPath, configPath) = CopyBaselineMiniFixture(makeCompliant: false);
        try
        {
            var exitCode = await AiNetLinter.Program.Main(["--path", rootPath, "--config", configPath]);
            Assert.NotEqual(0, exitCode);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task Main_AgainstFixtureWithSealedClass_ReturnsExitCodeZero()
    {
        var (rootPath, configPath) = CopyBaselineMiniFixture(makeCompliant: true);
        try
        {
            var exitCode = await AiNetLinter.Program.Main(["--path", rootPath, "--config", configPath]);
            Assert.Equal(0, exitCode);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static (string RootPath, string ConfigPath) CopyBaselineMiniFixture(bool makeCompliant)
    {
        var sourceRoot = Path.Combine(SolutionRootLocator.Find(), "tests", "Fixtures", "BaselineMini");
        var destinationRoot = Path.Combine(Path.GetTempPath(), $"ainetlinter-cli-adapter-{Guid.NewGuid():N}");
        CopyFixtureDirectory(sourceRoot, destinationRoot);

        var configPath = Path.Combine(destinationRoot, "rules.json");
        File.WriteAllText(configPath, PermissiveRulesJson);

        if (makeCompliant)
        {
            var violatingClassPath = Path.Combine(destinationRoot, "src", "BaselineMini", "ViolatingClass.cs");
            var content = File.ReadAllText(violatingClassPath);
            File.WriteAllText(
                violatingClassPath,
                content.Replace("public class ViolatingClass", "public sealed class ViolatingClass", StringComparison.Ordinal));
        }

        return (destinationRoot, configPath);
    }

    private static void CopyFixtureDirectory(string sourceRoot, string destinationRoot)
    {
        Directory.CreateDirectory(destinationRoot);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceRoot, sourceFile);
            var segments = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            if (Array.Exists(segments, s => s is "obj" or "bin"))
            {
                continue;
            }

            var targetFile = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);
        }
    }
}
