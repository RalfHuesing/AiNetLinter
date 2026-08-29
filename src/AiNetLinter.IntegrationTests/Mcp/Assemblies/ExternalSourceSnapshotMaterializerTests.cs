#nullable enable

using System.IO;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.TestKit;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Assemblies;

[Trait("Category", "Integration")]
public sealed class ExternalSourceSnapshotMaterializerTests
{
    [Fact]
    public async Task MaterializeAsync_LoadsLocalSolutionAndKeepsCheckoutUntilDispose()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(
            SolutionRootLocator.Find(),
            "BaselineMini",
            "external-source-materializer-fixture-");
        using var staging = TestTempDirectory.Create("external-source-materializer-staging-");
        var checkoutPath = staging.GetPath("checkout");
        Directory.Move(fixture.RootPath, checkoutPath);

        var ownershipToken = "materializer-integration-owner";
        var ownership = new ExternalSourceCheckoutOwnership(
            staging.DirectoryPath,
            checkoutPath,
            ownershipToken);
        File.WriteAllText(ownership.OwnershipMarkerPath, ownershipToken);
        var checkout = new ExternalSourceCheckoutHandle(
            ownership,
            Path.Combine(checkoutPath, "BaselineMini.slnx"),
            "revision-42");
        var mapping = new ExternalSourceMapping(
            "HTTPS://GITEA.EXAMPLE/shared.git",
            @".\BaselineMini.slnx",
            ["BaselineMini"]);
        var materializer = new ExternalSourceSnapshotMaterializer();

        var snapshot = await materializer.MaterializeAsync(mapping, checkout);

        Assert.Equal("https://gitea.example/shared.git", snapshot.Identity.RepositoryUrl);
        Assert.Equal("revision-42", snapshot.Identity.LoadedRevision);
        Assert.Equal("BaselineMini.slnx", snapshot.Identity.SolutionPath);
        var project = Assert.Single(snapshot.Solution.Projects);
        Assert.Equal("BaselineMini", project.AssemblyName);
        Assert.Contains(project.Documents, document =>
            string.Equals(document.Name, "ViolatingClass.cs", System.StringComparison.Ordinal));
        Assert.False(checkout.IsDisposed);
        Assert.True(Directory.Exists(checkoutPath));

        snapshot.Dispose();
        snapshot.Dispose();

        Assert.True(checkout.IsDisposed);
        Assert.Equal(ExternalSourceCheckoutCleanupState.Succeeded, checkout.CleanupState);
        Assert.False(Directory.Exists(checkoutPath));
    }

    [Fact]
    public async Task MaterializeAsync_MissingSolution_FailsWithoutAvailableSnapshot()
    {
        using var staging = TestTempDirectory.Create("external-source-materializer-invalid-");
        var checkoutPath = staging.CreateSubdirectory("checkout");
        var ownershipToken = "materializer-invalid-owner";
        var ownership = new ExternalSourceCheckoutOwnership(
            staging.DirectoryPath,
            checkoutPath,
            ownershipToken);
        File.WriteAllText(ownership.OwnershipMarkerPath, ownershipToken);
        var checkout = new ExternalSourceCheckoutHandle(
            ownership,
            Path.Combine(checkoutPath, "Missing.slnx"),
            "revision-42");
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "Missing.slnx",
            ["Missing"]);
        var materializer = new ExternalSourceSnapshotMaterializer();

        await Assert.ThrowsAsync<ExternalSourceSnapshotMaterializationException>(() =>
            materializer.MaterializeAsync(mapping, checkout).AsTask());

        Assert.False(checkout.IsDisposed);
        checkout.Dispose();
        Assert.False(Directory.Exists(checkoutPath));
    }
}
