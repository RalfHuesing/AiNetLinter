#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Mcp.Assemblies;

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
        var oldText = await Assert.Single(oldLease.Snapshot.Documents).GetTextAsync();

        AssemblyTestHelper.EmitAssembly(temp, "GenerationProbe", "namespace Probe; public sealed class Second { public int Value => 2; }");
        var refreshed = await session.RefreshAsync();
        var currentSnapshot = session.CurrentGeneration;
        Assert.NotNull(currentSnapshot);
        var current = currentSnapshot!;
        var currentText = await Assert.Single(current.Snapshot.Documents).GetTextAsync();

        Assert.False(refreshed.Reused);
        Assert.True(current.Number > oldGeneration);
        Assert.Contains("First", oldText.ToString(), StringComparison.Ordinal);
        Assert.Contains("Second", currentText.ToString(), StringComparison.Ordinal);
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
    public async Task RefreshAsync_RejectsOversizedAssemblyBeforeDecompilationAndDoesNotPublishCache()
    {
        using var temp = TestTempDirectory.Create("assembly-session-limits-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "LimitProbe", "namespace Probe; public sealed class Value { }");
        var options = new AssemblyDecompilationOptions(MaxAssemblyBytes: 1);
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, options, cacheRoot);

        var result = await session.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Failed, result.Status);
        Assert.Null(session.CurrentGeneration);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "assembly-size-limit");
        Assert.False(Directory.Exists(cacheRoot));
    }

    [Fact]
    public async Task RefreshAsync_CancellationFailsWithoutPublishingPartialGeneration()
    {
        using var temp = TestTempDirectory.Create("assembly-session-cancel-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(temp, "CancelProbe", "namespace Probe; public sealed class Value { }");
        var cacheRoot = temp.GetPath("cache");
        await using var session = new AssemblyAnalysisSession(assemblyPath, cacheRoot: cacheRoot);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await session.RefreshAsync(cancellation.Token);

        Assert.Equal(AssemblySessionStatus.Failed, result.Status);
        Assert.Null(session.CurrentGeneration);
        Assert.Empty(Directory.Exists(cacheRoot)
            ? Directory.EnumerateFiles(cacheRoot, "manifest.json", SearchOption.AllDirectories)
            : []);
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
    public async Task RefreshAsync_BudgetsNestedTypeTreesWithoutWholeModuleFallback()
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

        await using var typeLimited = new AssemblyAnalysisSession(
            assemblyPath,
            new AssemblyDecompilationOptions(MaxTypes: 1),
            temp.GetPath("type-cache"));
        var typeResult = await typeLimited.RefreshAsync();

        Assert.Equal(AssemblySessionStatus.Partial, typeResult.Status);
        Assert.NotNull(typeLimited.CurrentGeneration);
        Assert.Contains(typeResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-type-limit");
        Assert.DoesNotContain(Directory.Exists(temp.GetPath("type-cache"))
            ? Directory.EnumerateFiles(temp.GetPath("type-cache"), "*.cs", SearchOption.AllDirectories)
            : [],
            path => File.ReadAllText(path).Contains("First", StringComparison.Ordinal));

        await using var memberLimited = new AssemblyAnalysisSession(
            assemblyPath,
            new AssemblyDecompilationOptions(MaxMembers: 1),
            temp.GetPath("member-cache"));
        var memberResult = await memberLimited.RefreshAsync();
        Assert.Equal(AssemblySessionStatus.Failed, memberResult.Status);
        Assert.Contains(memberResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-member-limit");

        await using var complexityLimited = new AssemblyAnalysisSession(
            assemblyPath,
            new AssemblyDecompilationOptions(MaxComplexity: 1),
            temp.GetPath("complexity-cache"));
        var complexityResult = await complexityLimited.RefreshAsync();
        Assert.Equal(AssemblySessionStatus.Failed, complexityResult.Status);
        Assert.Contains(complexityResult.Diagnostics, diagnostic => diagnostic.Code == "assembly-complexity-limit");
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
    public void AssemblyDecompilationCache_DefaultRootUsesCacheContractPolicy()
    {
        var cache = new AssemblyDecompilationCache();
        var expected = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            AssemblyCacheContract.DefaultCacheDirectoryName,
            AssemblyCacheContract.DefaultAssemblyCacheDirectoryName));

        Assert.Equal(expected, AssemblyCacheContract.ResolveRootPath(null));
        Assert.Equal(expected, cache.RootPath);
    }

}
