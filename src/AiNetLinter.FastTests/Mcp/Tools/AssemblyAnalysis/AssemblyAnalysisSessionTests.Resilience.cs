#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisSessionTests
{
    [Theory]
    [InlineData(AssemblyDiagnosticSeverity.Warning, "warning")]
    [InlineData(AssemblyDiagnosticSeverity.Error, "error")]
    internal void AssemblyDiagnosticSeverity_WireFormatRoundtripsCorrectly(
        AssemblyDiagnosticSeverity severity,
        string wireValue)
    {
        var diagnostic = new AssemblySessionDiagnostic("assembly-test", "Testdiagnose", severity);

        Assert.Equal(severity, diagnostic.Severity);
        Assert.Equal(wireValue, severity.ToWireValue());
        Assert.True(AssemblyDiagnosticSeverityExtensions.TryParseWireValue(wireValue.ToUpperInvariant(), out var parsed));
        Assert.Equal(severity, parsed);
    }

    [Fact]
    public void AssemblyDiagnosticSeverity_WireParserRejectsUnknownValues()
    {
        Assert.False(AssemblyDiagnosticSeverityExtensions.TryParseWireValue("fatal", out _));
    }

    [Fact]
    public async Task RefreshAsync_CachedFileContainsSyntaxError_YieldsPartialStatusAndKeepsSnapshotQueryable()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cached-syntax-error-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SyntaxErrorProbe",
            """
            namespace Probe;
            public sealed class GoodClass
            {
                public int GetAnswer() => 42;
            }
            public sealed class AnotherClass
            {
                public string Name => "AiNetLinter";
            }
            """);
        var cacheRoot = temp.GetPath("cache");

        await using (var initialSession = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot))
        {
            var initialResult = await initialSession.RefreshAsync();
            Assert.Equal(AssemblySessionStatus.Complete, initialResult.Status);
        }

        var csFiles = Directory.EnumerateFiles(cacheRoot, "*.cs", SearchOption.AllDirectories).ToList();
        Assert.NotEmpty(csFiles);
        var fileToCorrupt = csFiles[0];
        File.AppendAllText(fileToCorrupt, Environment.NewLine + "syntax_error_not_valid_csharp !@#$;");

        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Partial, result.Status);
        Assert.NotNull(session.CurrentGeneration);
        Assert.Equal(AssemblySessionStatus.Partial, session.CurrentGeneration!.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "assembly-workspace-compilation-failed"
            || diagnostic.Message.Contains("Syntaxfehler", StringComparison.OrdinalIgnoreCase)
            || diagnostic.Message.Contains("nicht parsbaren Quelltext", StringComparison.OrdinalIgnoreCase));

        var leaseSnapshot = session.AcquireSnapshot();
        Assert.NotNull(leaseSnapshot);
        using var lease = leaseSnapshot!;
        var compilation = lease.Snapshot.Compilation;
        Assert.NotNull(compilation);

        var goodType = compilation.GetTypeByMetadataName("Probe.GoodClass")
            ?? compilation.GetTypeByMetadataName("Probe.AnotherClass");
        Assert.NotNull(goodType);
    }

    [Fact]
    public async Task RefreshAsync_FreshDecompilationWithSyntaxError_YieldsPartialStatusAndKeepsQueryableSnapshot()
    {
        using var temp = TestTempDirectory.Create("assembly-session-fresh-syntax-error-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "FreshSyntaxProbe",
            """
            namespace Probe;
            public sealed class WorkingType
            {
                public int Calculate() => 100;
            }
            """);
        var cacheRoot = temp.GetPath("cache");

        var baseAdapter = new AssemblyDecompilationAdapter();
        var testAdapter = new AssemblyDecompilationAdapter(async (request, references) =>
        {
            var result = await baseAdapter.DecompileAsync(request, references);
            if (result.Documents.Count == 0 || request.StagingDirectory is null) return result;

            var brokenFilePath = Path.Combine(request.StagingDirectory, "BrokenType.cs");
            var brokenSource = "namespace Probe; public class BrokenClass { broken syntax !@#$; }";
            File.WriteAllText(brokenFilePath, brokenSource);

            var updatedDocuments = result.Documents
                .Append(new DecompiledDocument(brokenFilePath, "BrokenClass", brokenSource))
                .ToList();

            var diagnostics = result.Diagnostics
                .Append(new AssemblySessionDiagnostic(
                    AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)),
                    "Die dekompilierte Datei 'BrokenType.cs' enthält Syntaxfehler: CS1002 ; expected.",
                    AssemblyDiagnosticSeverity.Warning))
                .ToList();

            return new DecompilationResult(updatedDocuments, diagnostics, true, result.ProjectFilePath);
        });
        var options = new AssemblyAnalysisSessionOptions(assemblyPath, AssemblyDecompilationOptions.Default, cacheRoot);
        await using var session = new AssemblyAnalysisSession(options, decompilationAdapter: testAdapter);

        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Partial, result.Status);
        Assert.NotNull(session.CurrentGeneration);
        Assert.Equal(AssemblySessionStatus.Partial, session.CurrentGeneration!.Status);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code == "assembly-type-decompilation-empty"
            || diagnostic.Code == "assembly-workspace-compilation-failed"
            || diagnostic.Message.Contains("Syntaxfehler", StringComparison.OrdinalIgnoreCase));

        var leaseSnapshot = session.AcquireSnapshot();
        Assert.NotNull(leaseSnapshot);
        using var lease = leaseSnapshot!;
        var workingType = lease.Snapshot.Compilation.GetTypeByMetadataName("Probe.WorkingType");
        Assert.NotNull(workingType);
    }

    [Fact]
    public async Task RefreshAsync_CachedFileIsEmpty_YieldsPartialStatusAndReadsFromCacheWithoutException()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cached-empty-file-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "EmptyFileProbe",
            """
            namespace Probe;
            public sealed class NormalClass
            {
                public int GetAnswer() => 42;
            }
            """);
        var cacheRoot = temp.GetPath("cache");

        var baseAdapter = new AssemblyDecompilationAdapter();
        var testAdapter = new AssemblyDecompilationAdapter(async (request, references) =>
        {
            var result = await baseAdapter.DecompileAsync(request, references);
            if (result.Documents.Count == 0 || request.StagingDirectory is null) return result;

            var emptyFilePath = Path.Combine(request.StagingDirectory, "EmptyStub.cs");
            File.WriteAllText(emptyFilePath, string.Empty);

            var updatedDocuments = result.Documents
                .Append(new DecompiledDocument(emptyFilePath, "EmptyStub", string.Empty))
                .ToList();

            var diagnostics = result.Diagnostics
                .Append(new AssemblySessionDiagnostic(
                    AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)),
                    "Die dekompilierte Datei 'EmptyStub.cs' ist leer.",
                    AssemblyDiagnosticSeverity.Warning))
                .ToList();

            return new DecompilationResult(updatedDocuments, diagnostics, true, result.ProjectFilePath);
        });

        var options = new AssemblyAnalysisSessionOptions(assemblyPath, AssemblyDecompilationOptions.Default, cacheRoot);
        await using (var initialSession = new AssemblyAnalysisSession(options, decompilationAdapter: testAdapter))
        {
            var initialResult = await initialSession.RefreshAsync();
            Assert.Equal(AssemblySessionStatus.Partial, initialResult.Status);
        }

        // New session on same cache - must read from cache without throwing InvalidDataException
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Partial, result.Status);
        Assert.NotNull(session.CurrentGeneration);
        Assert.Equal(AssemblySessionStatus.Partial, session.CurrentGeneration!.Status);
        Assert.Contains(session.CurrentGeneration!.Snapshot.Documents, doc => doc.Name == "EmptyStub.cs");

        var leaseSnapshot = session.AcquireSnapshot();
        Assert.NotNull(leaseSnapshot);
        using var lease = leaseSnapshot!;
        var normalType = lease.Snapshot.Compilation.GetTypeByMetadataName("Probe.NormalClass");
        Assert.NotNull(normalType);
    }

    [Fact]
    public void AssemblyCacheGenerationStorage_FindProjectFile_NonExistentDirectory_ReturnsNullWithoutException()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), "non-existent-" + Guid.NewGuid().ToString("N"));
        var result = AiNetLinter.Mcp.Assemblies.Analysis.Coordinators.AssemblyCacheGenerationStorage.FindProjectFile(nonExistentPath);
        Assert.Null(result);
    }
}
