#nullable enable

using System;
using System.Linq;
using AiNetLinter.Baseline;
using AiNetLinter.Mcp;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

[Trait("Category", "Component")]
public sealed class McpCodeGraphServerReadOnlySnapshotTests
{
    [Fact]
    public void GetCurrentSolution_ReadOnlySnapshotWithVirtualDocumentPath_KeepsDocumentAcrossCalls()
    {
        using var snapshot = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Snapshot.slnx",
            new ProjectSpec("SnapshotProject", [("Virtual.cs", "namespace Snapshot; public class VirtualType {}")],
                VirtualProjectDirectory: "src/SnapshotProject"));
        using var server = CreateServer(snapshot.Solution);

        var first = server.GetCurrentSolution();
        var second = server.GetCurrentSolution();

        Assert.NotNull(first);
        Assert.Same(first, second);
        var document = Assert.Single(first!.Projects.Single().Documents);
        Assert.Equal(@"C:\ainetlinter-virtual\src\SnapshotProject\Virtual.cs", document.FilePath);
    }

    [Fact]
    public void GetCurrentSolution_ReadOnlySnapshot_DoesNotRefresh()
    {
        using var snapshot = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Snapshot.slnx",
            new ProjectSpec("SnapshotProject", [("Virtual.cs", "namespace Snapshot; public class VirtualType {}")],
                VirtualProjectDirectory: "src/SnapshotProject"));
        using var server = CreateServer(snapshot.Solution);

        _ = server.GetCurrentSolution();
        _ = server.GetCurrentSolution();

        Assert.Equal(0, server.RefreshCount);
    }

    [Fact]
    public void Constructor_ReadOnlySnapshotCombinedWithCatalog_ThrowsArgumentException()
    {
        using var snapshot = RoslynTestSolutionFactory.CreateSolution(
            @"C:\ainetlinter-virtual\Snapshot.slnx",
            new ProjectSpec("SnapshotProject", [("Virtual.cs", "namespace Snapshot; public class VirtualType {}")],
                VirtualProjectDirectory: "src/SnapshotProject"));
        var options = McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            new SourceFileCatalog(snapshot.Solution, hasLoadingErrors: false),
            ReadOnlySolutionSnapshot: snapshot.Solution));

        var exception = Assert.Throws<ArgumentException>(() => new McpCodeGraphServer(options));

        Assert.Contains("ReadOnlySolutionSnapshot", exception.Message, StringComparison.Ordinal);
    }

    private static McpCodeGraphServer CreateServer(Microsoft.CodeAnalysis.Solution snapshot) => new(
        McpCodeGraphServerOptions.From(new McpCodeGraphServerOptionsFromParameters(
            null,
            ReadOnlySolutionSnapshot: snapshot)));
}
