#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

[Trait("Category", "Component")]
// @covers AssemblyDecompilationAdapter
// @covers AssemblyDecompilationCache
// @covers AssemblyReferenceResolver
// @covers AssemblyRoslynWorkspaceFactory
public sealed class AssemblyAnalysisSessionTests
{
    [Fact]
    public async Task RefreshAsync_ReusesGenerationWhenOnlyMtimeChanges()
    {
        using var temp = TestTempDirectory.Create("assembly-session-fingerprint-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "MtimeProbe", "namespace Probe; public sealed class Value { public int Number => 1; }");
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);

        var first = await session.RefreshAsync();
        var firstGenerationSnapshot = session.CurrentGeneration;
        Assert.NotNull(firstGenerationSnapshot);
        var firstGeneration = firstGenerationSnapshot!.Number;
        var originalMtime = File.GetLastWriteTimeUtc(assemblyPath);
        File.SetLastWriteTimeUtc(assemblyPath, originalMtime.AddMinutes(1));

        var second = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Complete, first.Status);
        Assert.True(second.Reused);
        Assert.Equal(firstGeneration, second.Generation);
        Assert.Equal(AssemblySessionStatus.Complete, session.State.Status);
        Assert.Equal(originalMtime.AddMinutes(1), session.State.Fingerprint!.MtimeUtc);
    }

    [Fact]
    public async Task RefreshAsync_EagerlyMaterializesWholeProjectWithRealSourceFiles()
    {
        using var temp = TestTempDirectory.Create("assembly-session-signature-only-");
        var methods = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 160)
                .Select(index => $"public int Read{index:D3}(int value) => value + {index};"));
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SignatureOnlyProbe",
            $"namespace Probe; public sealed class Value {{ {methods} }}");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: temp.GetPath("cache"));

        var result = await session.RefreshAsync();
        var generation = session.CurrentGeneration;
        Assert.NotNull(generation);
        var sources = await Task.WhenAll(generation.Snapshot.Documents.Select(document => document.GetTextAsync()));
        var source = string.Join(Environment.NewLine, sources.Select(text => text.ToString()));
        var diagnostics = generation.Snapshot.Compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();

        Assert.Equal(AssemblySessionStatus.Complete, result.Status);
        Assert.Contains("Read159", source, StringComparison.Ordinal);
        Assert.Contains("value + 159", source, StringComparison.Ordinal);
        Assert.NotEmpty(generation.Snapshot.Documents);
        Assert.All(generation.Snapshot.Documents, document => Assert.True(File.Exists(document.FilePath)));
        Assert.Single(Directory.EnumerateFiles(temp.GetPath("cache"), "*.csproj", SearchOption.AllDirectories));
        Assert.DoesNotContain("throw null!;", source, StringComparison.Ordinal);
        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == "CS0501");
    }

    [Fact]
    public async Task RefreshAsync_ChangesGenerationForChangedBytesAndKeepsOldLeaseReadable()
    {
        using var temp = TestTempDirectory.Create("assembly-session-generation-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "GenerationProbe", "namespace Probe; public sealed class First { public int Value => 1; }");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: temp.GetPath("cache"));

        await session.RefreshAsync();
        var oldLeaseSnapshot = session.AcquireSnapshot();
        Assert.NotNull(oldLeaseSnapshot);
        using var oldLease = oldLeaseSnapshot!;
        var oldGeneration = oldLease.Generation.Number;
        var oldText = string.Join(
            Environment.NewLine,
            (await Task.WhenAll(oldLease.Snapshot.Documents.Select(document => document.GetTextAsync())))
                .Select(text => text.ToString()));

        AssemblyTestHelper.EmitAssembly(temp, "GenerationProbe", "namespace Probe; public sealed class Second { public int Value => 2; }");
        var refreshed = await session.RefreshAsync();
        var currentSnapshot = session.CurrentGeneration;
        Assert.NotNull(currentSnapshot);
        var current = currentSnapshot!;
        var currentText = string.Join(
            Environment.NewLine,
            (await Task.WhenAll(current.Snapshot.Documents.Select(document => document.GetTextAsync())))
                .Select(text => text.ToString()));

        Assert.False(refreshed.Reused);
        Assert.True(current.Number > oldGeneration);
        Assert.Contains("First", oldText, StringComparison.Ordinal);
        Assert.Contains("Second", currentText, StringComparison.Ordinal);
        Assert.Equal("decompiled", current.Origin.OriginKind);
        Assert.Contains(current.Snapshot.Origins.Values, origin => origin.ContentHash == current.Fingerprint.Sha256);
    }

    [Fact]
    public async Task RefreshAsync_PublishesManifestAndNewSessionReadsTheCache()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cache-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CacheProbe", "namespace Probe; public sealed class Cached { public string Name => \"cached\"; }");
        var cacheRoot = temp.GetPath("cache");
        await using (var first = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot))
        {
            var result = await first.RefreshAsync();
            Assert.Equal(AssemblySessionStatus.Complete, result.Status);
        }

        var manifestPath = Assert.Single(Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories));
        var pointerPath = Assert.Single(Directory.EnumerateFiles(cacheRoot, "current.json", SearchOption.AllDirectories));
        using var pointer = JsonDocument.Parse(File.ReadAllText(pointerPath));
        var generationPath = Path.Combine(Path.GetDirectoryName(pointerPath)!, pointer.RootElement.GetProperty("generation").GetString()!);
        Assert.Equal(Path.GetDirectoryName(manifestPath), generationPath, StringComparer.OrdinalIgnoreCase);
        var manifest = File.ReadAllText(manifestPath);
        using var manifestDocument = JsonDocument.Parse(manifest);
        Assert.Equal(
            new[] { "assemblyIdentity", "cacheKey", "cacheSchemaVersion", "complete", "createdUtc", "decompilerVersion", "encoding", "errors", "generatedFiles", "lastAccessUtc", "length", "mtimeUtc", "optionsIdentity", "originalPath", "references", "sha256", "status", "unresolvedReferences", "warnings", "canonicalPath" }.OrderBy(value => value),
            manifestDocument.RootElement.EnumerateObject().Select(property => property.Name).OrderBy(value => value));
        Assert.Contains("\"status\": \"complete\"", manifest, StringComparison.Ordinal);
        Assert.Contains("generatedFiles", manifest, StringComparison.Ordinal);
        Assert.Contains("assembly-cache-v2", manifest, StringComparison.Ordinal);

        await using var second = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        var cached = await second.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Complete, cached.Status);
        Assert.False(cached.Reused);
        Assert.NotNull(second.CurrentGeneration);
        Assert.All(second.CurrentGeneration!.Snapshot.Documents, document => Assert.True(File.Exists(document.FilePath)));
        Assert.Equal(
            Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(manifestPath)!, "*.csproj")),
            Assert.Single(second.CurrentGeneration.Snapshot.Solution.Projects).FilePath,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_ReplacesIncompatibleManifestBeforePublishingNewGeneration()
    {
        using var temp = TestTempDirectory.Create("assembly-session-manifest-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "ManifestProbe", "namespace Probe; public sealed class Stable { }");
        var cacheRoot = temp.GetPath("cache");
        await using (var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot))
        {
            await session.RefreshAsync();
        }
        var manifestPath = Assert.Single(Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories));
        File.WriteAllText(manifestPath, File.ReadAllText(manifestPath).Replace("assembly-cache-v2", "wrong-schema", StringComparison.Ordinal));

        await using var refreshedSession = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        var refreshed = await refreshedSession.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Complete, refreshed.Status);
        Assert.Contains(refreshed.Diagnostics, diagnostic => diagnostic.Code == "assembly-cache-invalid");
        Assert.Contains(Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories), path =>
            File.ReadAllText(path).Contains("assembly-cache-v2", StringComparison.Ordinal));
        Assert.Empty(Directory.EnumerateDirectories(cacheRoot, "*.retired-*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RefreshAsync_MissingReferenceIsVisibleAsPartialWithoutLoadingTargetAssembly()
    {
        using var temp = TestTempDirectory.Create("assembly-session-partial-");
        var dependencyPath = AssemblyTestHelper.EmitAssembly(temp, "SessionDependency", "namespace Dependency; public sealed class Value { }");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "SessionTarget",
            "namespace Probe; public sealed class UsesDependency { public Dependency.Value Value { get; } = new(); }",
            dependencyPath);
        File.Delete(dependencyPath);
        var loadedBefore = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.Location).ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: temp.GetPath("cache"));
        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Partial, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-reference-unresolved");
        Assert.NotNull(session.CurrentGeneration);
        Assert.DoesNotContain(assemblyPath, AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.Location), StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(assemblyPath, loadedBefore, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_RemovesArtificialWholeAssemblyLimits()
    {
        using var temp = TestTempDirectory.Create("assembly-session-limits-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "LimitProbe", "namespace Probe; public sealed class Value { }");
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);

        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Complete, result.Status);
        Assert.NotNull(session.CurrentGeneration);
        Assert.NotEmpty(Directory.EnumerateFiles(cacheRoot, "*.cs", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RefreshAsync_CancellationThrowsAndDoesNotPublishPartialGeneration()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cancel-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CancelProbe", "namespace Probe; public sealed class Value { }");
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RefreshAsync(cancellation.Token));

        Assert.Null(session.CurrentGeneration);
        Assert.Empty(Directory.Exists(cacheRoot)
            ? Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories)
            : []);
        Assert.Empty(Directory.Exists(cacheRoot)
            ? Directory.EnumerateDirectories(cacheRoot, "*.tmp", SearchOption.AllDirectories)
            : []);
    }

    [Fact]
    public async Task RefreshAsync_SubsequentRefreshAfterCancellation_Succeeds()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cancel-retry-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CancelRetryProbe", "namespace Probe; public sealed class Value { public int Number => 42; }");
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => session.RefreshAsync(cancellation.Token));

        Assert.Null(session.CurrentGeneration);

        var result = await session.RefreshAsync(CancellationToken.None);

        Assert.Equal(AssemblySessionStatus.Complete, result.Status);
        Assert.NotNull(session.CurrentGeneration);
        Assert.Equal(AssemblySessionStatus.Complete, session.State.Status);
        Assert.NotEmpty(Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task RefreshAsync_RejectsTimeoutOutsideCancelAfterRangeWithoutThrowing()
    {
        using var temp = TestTempDirectory.Create("assembly-session-timeout-range-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TimeoutRangeProbe",
            "namespace Probe; public sealed class Value { public int Number => 42; }");
        await using var session = new AssemblyAnalysisSession(
            assemblyPath,
            new AssemblyDecompilationOptions(
                Timeout: AssemblyDecompilationOptions.MaxCancelAfterTimeout + TimeSpan.FromMilliseconds(1)),
            temp.GetPath("cache"));

        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Failed, result.Status);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-options-invalid");
        Assert.Null(session.CurrentGeneration);
    }

    [Fact]
    public async Task RefreshAsync_PreservesLastGoodSnapshotAsDegradedAfterRefreshFailure()
    {
        using var temp = TestTempDirectory.Create("assembly-session-degraded-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "DegradedProbe", "namespace Probe; public sealed class Value { }");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: temp.GetPath("cache"));
        var initial = await session.RefreshAsync();
        var initialGeneration = session.CurrentGeneration;
        Assert.NotNull(initialGeneration);
        File.Delete(assemblyPath);

        var failedRefresh = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Complete, initial.Status);
        Assert.Equal(AssemblySessionStatus.Degraded, failedRefresh.Status);
        Assert.Equal(initialGeneration!.Number, failedRefresh.Generation);
        Assert.NotNull(session.CurrentGeneration);
        Assert.Equal(initialGeneration.Number, session.CurrentGeneration!.Number);
        Assert.Contains(failedRefresh.Diagnostics, diagnostic => diagnostic.Code == "assembly-fingerprint-failed");
    }

    [Fact]
    public async Task RefreshAsync_EagerlyMaterializesNestedTypesWithoutTypeBudgets()
    {
        using var temp = TestTempDirectory.Create("assembly-session-nested-limits-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "NestedLimitProbe", """
            namespace Probe;
            public sealed class First
            {
                public int Value { get; set; }
                public event System.EventHandler? Changed;
                public void Execute() { Changed?.Invoke(this, System.EventArgs.Empty); }
                public sealed class Nested { public int NestedValue; }
            }
            public sealed class Second { public int Value => 2; }
            """);

        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: temp.GetPath("eager-cache"));
        var result = await session.RefreshAsync();
        var source = string.Join(
            Environment.NewLine,
            (await Task.WhenAll(session.CurrentGeneration!.Snapshot.Documents.Select(document => document.GetTextAsync())))
                .Select(text => text.ToString()));

        Assert.Equal(AssemblySessionStatus.Complete, result.Status);
        Assert.Contains("First", source, StringComparison.Ordinal);
        Assert.Contains("Nested", source, StringComparison.Ordinal);
        Assert.Contains("Second", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AssemblyDiagnosticSeverityExtensions.WarningWireValue)]
    [InlineData(AssemblyDiagnosticSeverityExtensions.ErrorWireValue)]
    public void AssemblySessionDiagnostic_UsesTypedSeverityAndWireValues(string wireValue)
    {
        Assert.True(AssemblyDiagnosticSeverityExtensions.TryParseWireValue(wireValue, out var severity));
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

}
