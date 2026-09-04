#nullable enable

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp.Assemblies;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Assemblies;

[Trait("Category", "Component")]
public sealed class ExternalSourceRepositoryReadOnlyCleanupTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task CheckoutHandle_DisposeClearsReadOnlyAttributesBeforeDeletion()
    {
        using var fixture = IsolatedFixtureLease.CopyFixture(SolutionRootLocator.Find(), "BaselineMini");
        using var staging = TestTempDirectory.Create("external-source-acquirer-readonly-cleanup-");
        var transport = new ExternalSourceRecordingTransport((_, destination, _) =>
        {
            ExternalSourceRepositoryFixtureOperations.CopyBaselineMiniSolution(fixture.RootPath, destination);
            return ExternalSourceRepositoryTestTransportResults.Success(destination, Revision);
        });
        var acquirer = ExternalSourceRepositoryTestFactory.CreateAcquirer(transport, staging);
        var result = await acquirer.AcquireAsync(new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "BaselineMini.slnx",
            ["BaselineMini"]));
        var checkout = Assert.IsType<ExternalSourceCheckoutHandle>(result.Checkout);
        var nestedFile = Directory.EnumerateFiles(checkout.CheckoutPath, "*", SearchOption.AllDirectories).First();
        File.SetAttributes(nestedFile, File.GetAttributes(nestedFile) | FileAttributes.ReadOnly);
        File.SetAttributes(checkout.CheckoutPath, File.GetAttributes(checkout.CheckoutPath) | FileAttributes.ReadOnly);

        checkout.Dispose();

        Assert.Equal(ExternalSourceCheckoutCleanupState.Succeeded, checkout.CleanupState);
        Assert.False(Directory.Exists(checkout.CheckoutPath));
    }
}
