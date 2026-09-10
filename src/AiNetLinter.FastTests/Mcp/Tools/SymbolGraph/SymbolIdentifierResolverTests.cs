#nullable enable

using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Handoffs;
using AiNetLinter.Mcp.Tools.SymbolGraph;
using AiNetLinter.TestKit;
using Microsoft.CodeAnalysis;
using System.Linq;
using System.Threading;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.SymbolGraph;

/// <summary>
/// Reine Parsing-Tests fuer <see cref="SymbolIdentifierResolver.TryParsePosition"/> und
/// <see cref="SymbolIdentifierResolver.TryParseLineOnlyPosition"/> — keine Solution/Fixture
/// noetig, da beide Methoden nur Strings segmentieren.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SymbolIdentifierResolverTests
{
    [Fact]
    public async Task TryResolveByStableIdAsync_CurrentAssemblyIdentityResolvesWrappedId()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { public void Run() { } }");
        var project = owner.Solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var symbol = compilation.GetTypeByMetadataName("Probe.Current")!;
        var rawId = DocumentationCommentId.CreateDeclarationId(symbol)!;
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('a', 64),
            generation: 4);

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            identity.Format(rawId)!,
            CancellationToken.None,
            identity);

        Assert.Null(error);
        Assert.NotNull(resolved);
        Assert.Equal(rawId, DocumentationCommentId.CreateDeclarationId(resolved));
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_CurrentAssemblyIdentityResolvesBareDocumentationId()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { public void Run() { } }");
        var project = owner.Solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var symbol = compilation.GetTypeByMetadataName("Probe.Current")!;
        var rawId = DocumentationCommentId.CreateDeclarationId(symbol)!;
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('d', 64),
            generation: 7);

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            rawId,
            CancellationToken.None,
            identity);

        Assert.Null(error);
        Assert.NotNull(resolved);
        Assert.Equal(rawId, DocumentationCommentId.CreateDeclarationId(resolved));
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_StaleAssemblyIdentityIsRejected()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { public void Run() { } }");
        var project = owner.Solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var symbol = compilation.GetTypeByMetadataName("Probe.Current")!;
        var rawId = DocumentationCommentId.CreateDeclarationId(symbol)!;
        var currentIdentity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('b', 64),
            generation: 5);
        var staleId = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('a', 64),
            generation: 4).Format(rawId)!;

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            staleId,
            CancellationToken.None,
            currentIdentity);

        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.Contains(
            "STALE_SNAPSHOT",
            Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(error!.Content)).Text);
    }

    [Fact]
    public void AnalysisSymbolIdentity_AssemblyFormatUsesOpaqueTokensWithoutGeneration()
    {
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('a', 64),
            generation: 42);

        var id = identity.Format("T:Probe.Type")!;

        Assert.StartsWith("a:", id, System.StringComparison.Ordinal);
        Assert.DoesNotContain(":42:", id, System.StringComparison.Ordinal);
        Assert.True(SymbolHandoffIdentifier.TryParse(id, out var parsed));
        Assert.Equal(SymbolHandoffOrigin.Assembly, parsed.Origin);
        Assert.Equal("T:Probe.Type", parsed.DocumentationCommentId);
        Assert.Equal(22, parsed.TargetToken.Length);
        Assert.Equal(22, parsed.ContentToken.Length);
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_SourceHandoffDistinguishesTargetAndSnapshot()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { public void Run() { } }");
        var rawId = "T:Probe.Current";
        var current = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('b', 64));

        var (_, targetError) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            AnalysisSymbolIdentity.ForSource(@"C:\other\workspace.slnx", new string('b', 64)).Format(rawId)!,
            CancellationToken.None,
            current);
        var (_, staleError) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            AnalysisSymbolIdentity.ForSource(@"C:\current\workspace.slnx", new string('a', 64)).Format(rawId)!,
            CancellationToken.None,
            current);

        Assert.Contains("TARGET_MISMATCH", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(targetError!.Content)).Text);
        Assert.Contains("STALE_SNAPSHOT", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(staleError!.Content)).Text);
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_SourceHandoffDoesNotChooseFirstAmbiguousDeclaration()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            new ProjectSpec("First", [("First.cs", "namespace Probe; public sealed class Current { }")]),
            new ProjectSpec("Second", [("Second.cs", "namespace Probe; public sealed class Current { }")]));
        var firstProject = owner.Solution.Projects.Single(project => project.Name == "First");
        var compilation = (await firstProject.GetCompilationAsync())!;
        var rawId = DocumentationCommentId.CreateDeclarationId(
            compilation.GetTypeByMetadataName("Probe.Current")!)!;
        var identity = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('b', 64));

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            identity.Format(rawId)!,
            CancellationToken.None,
            identity);

        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.Equal(
            "AMBIGUOUS_SYMBOL",
            error!.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_SourceHandoffMissingDeclarationReturnsSymbolNotFound()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { }");
        var identity = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('b', 64));

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            identity.Format("T:Probe.Missing")!,
            CancellationToken.None,
            identity);

        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.Equal(
            "SYMBOL_NOT_FOUND",
            error!.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_HandoffErrorsDoNotEchoLongIdentifier()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { }");
        var current = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('b', 64));
        var longDocumentationId = "M:Probe.Current.Run(" + string.Join(",", Enumerable.Repeat("System.String", 20)) + ")";
        var handoff = AnalysisSymbolIdentity.ForSource(
            @"C:\other\workspace.slnx",
            new string('b', 64)).Format(longDocumentationId)!;

        var (_, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            handoff,
            CancellationToken.None,
            current);

        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(error!.Content)).Text;
        Assert.DoesNotContain(handoff, text, StringComparison.Ordinal);
        Assert.Contains("TARGET_MISMATCH", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("x:aaaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:T:Probe.Current")]
    [InlineData("source:legacy:legacy:T:Probe.Current")]
    [InlineData("assembly:legacy:legacy:T:Probe.Current")]
    [InlineData("s:aaaaaaaaaaaaaaaaaaaaa:bbbbbbbbbbbbbbbbbbbbbb:T:Probe.Current")]
    public async Task TryResolveByStableIdAsync_NonCanonicalHandoffLikeInputIsInvalidArgument(string identifier)
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { } ");
        var current = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('a', 64));

        var (_, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            identifier,
            CancellationToken.None,
            current);

        Assert.NotNull(error);
        Assert.Equal(
            "INVALID_ARGUMENT",
            error!.StructuredContent!.Value.GetProperty("code").GetString());
        Assert.Equal(
            "$.symbolIdentifier",
            error.StructuredContent.Value.GetProperty("fieldPath").GetString());
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_WrappedQualifiedNameIsInvalidArgument()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { } ");
        var current = AnalysisSymbolIdentity.ForSource(
            @"C:\current\workspace.slnx",
            new string('a', 64));
        var nonCanonical = current.Format("T:Probe.Current")!.Replace(
            ":T:Probe.Current",
            ":Probe.Current",
            System.StringComparison.Ordinal);

        var (_, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            nonCanonical,
            CancellationToken.None,
            current);

        Assert.NotNull(error);
        Assert.Equal(
            "INVALID_ARGUMENT",
            error!.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_NonCanonicalAssemblyHandoffIsRejected()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Current { public void Run(Missing.Type value) { } }");
        var project = owner.Solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var symbol = compilation.GetTypeByMetadataName("Probe.Current")!
            .GetMembers("Run")
            .OfType<IMethodSymbol>()
            .Single();
        var rawId = DocumentationCommentId.CreateDeclarationId(symbol)!;
        var identity = AnalysisSymbolIdentity.ForAssembly(
            @"C:\Assemblies\Probe.dll",
            new string('c', 64),
            generation: 6);

        var (resolved, error) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution,
            identity.Format(rawId)!.Replace(
                rawId,
                rawId + "#lf:Local",
                StringComparison.Ordinal),
            CancellationToken.None,
            identity);

        Assert.Null(resolved);
        Assert.NotNull(error);
        Assert.Equal(
            "INVALID_ARGUMENT",
            error!.StructuredContent!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public void TryParsePosition_FileLineColumn_ReturnsTrueWithParsedSegments()
    {
        var ok = SymbolIdentifierResolver.TryParsePosition("src/Foo.cs:42:10", out var path, out var line, out var column);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs", path);
        Assert.Equal(42, line);
        Assert.Equal(10, column);
    }

    [Fact]
    public void TryParsePosition_FileLineOnly_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParsePosition("src/Foo.cs:42", out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParsePosition_WindowsDriveLetterPathWithColumn_ReturnsTrueAndReassemblesDriveLetter()
    {
        // Regression: ein Laufwerksbuchstabe erzeugt beim Split durch ':' ein zusaetzliches
        // Segment ("C", "\Foo.cs", "91", "5") — die letzten zwei Segmente sind trotzdem Zeile/
        // Spalte, der Rest (inkl. ':') wird als Pfad wieder zusammengesetzt.
        var ok = SymbolIdentifierResolver.TryParsePosition("C:\\Foo.cs:91:5", out var path, out var line, out var column);

        Assert.True(ok);
        Assert.Equal("C:\\Foo.cs", path);
        Assert.Equal(91, line);
        Assert.Equal(5, column);
    }

    [Fact]
    public void TryParsePosition_WindowsDriveLetterPathWithLineOnly_ReturnsFalse()
    {
        // "C:\Datei.cs:91" hat nach Split durch ':' drei Segmente ("C", "\Datei.cs", "91") — das
        // vorletzte Segment ("\Datei.cs") ist keine Ganzzahl, TryParsePosition lehnt korrekt ab.
        var ok = SymbolIdentifierResolver.TryParsePosition("C:\\Datei.cs:91", out _, out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseLineOnlyPosition_FileLine_ReturnsTrueWithParsedSegments()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("src/Foo.cs:42", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs", path);
        Assert.Equal(42, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_FileLineColumn_JoinsLeadingSegmentsAsPath()
    {
        // TryParseLineOnlyPosition wird nur aufgerufen, wenn TryParsePosition bereits
        // fehlgeschlagen ist (Aufrufer-Reihenfolge in FindReferencesTool) — isoliert betrachtet
        // parst die Methode aber immer "von hinten": letztes Segment = Zeile, Rest = Pfad
        // (inkl. enthaltener ':'). Dokumentiert diesen Mechanismus explizit.
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("src/Foo.cs:42:10", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("src/Foo.cs:42", path);
        Assert.Equal(10, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_WindowsDriveLetterPathWithLineOnly_ReconstructsDriveLetterPath()
    {
        // Ein Windows-Pfad mit Laufwerksbuchstabe enthält nach dem Split durch ':' drei Segmente.
        // Von hinten geparst (letztes Segment = Zeile) bleibt der Laufwerksbuchstabe Teil des Pfads.
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("C:\\Datei.cs:91", out var path, out var line);

        Assert.True(ok);
        Assert.Equal("C:\\Datei.cs", path);
        Assert.Equal(91, line);
    }

    [Fact]
    public void TryParseLineOnlyPosition_QualifiedNameWithoutColon_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("Namespace.Klasse.Methode", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public void TryParseLineOnlyPosition_NonNumericLastSegment_ReturnsFalse()
    {
        var ok = SymbolIdentifierResolver.TryParseLineOnlyPosition("Klasse.Methode:xyz", out _, out _);

        Assert.False(ok);
    }

    [Fact]
    public async Task TryResolveByStableIdAsync_WithAndWithoutReturnTypeSuffix_BothResolve()
    {
        using var owner = RoslynTestSolutionFactory.CreateSolution(
            "namespace Probe; public sealed class Service { public string Process(int id) => id.ToString(); }");
        var project = owner.Solution.Projects.Single();
        var compilation = (await project.GetCompilationAsync())!;
        var symbol = compilation.GetTypeByMetadataName("Probe.Service")!
            .GetMembers("Process")
            .OfType<IMethodSymbol>()
            .Single();
        var rawId = DocumentationCommentId.CreateDeclarationId(symbol)!;
        var withReturnType = rawId + "~System.String";

        var (resolvedExact, errorExact) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution, rawId, CancellationToken.None);
        var (resolvedWithReturn, errorWithReturn) = await SymbolIdentifierResolver.TryResolveByStableIdAsync(
            owner.Solution, withReturnType, CancellationToken.None);

        Assert.Null(errorExact);
        Assert.NotNull(resolvedExact);
        Assert.Null(errorWithReturn);
        Assert.NotNull(resolvedWithReturn);
        Assert.True(SymbolEqualityComparer.Default.Equals(symbol, resolvedExact));
        Assert.True(SymbolEqualityComparer.Default.Equals(symbol, resolvedWithReturn));
    }
}
