#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace AiNetLinter.FastTests.Mcp.Daemon;

// @covers DaemonMcpSession
[Trait("Category", "Component")]
public sealed class DaemonHostMcpContractTests
{
    [Fact]
    public async Task RunMcpSessionAsync_UsesTheExistingMcpSessionRunnerOnConnectionEof()
    {
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        await using var connection = new DaemonPipeConnection(new MemoryStream());
        using var composition = AssemblyAnalysisHostComposition.Create();

        var session = CreateSession(registry, composition);
        await session.RunAsync(connection);

        Assert.False(connection.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task RunMcpSessionAsync_RegisteredAssemblyToolsReuseCompositionAcrossSessions()
    {
        using var temp = TestTempDirectory.Create("daemon-mcp-host-composition-");
        var assemblyPath = AssemblyTestHelper.EmitAssembly(
            temp,
            "TargetAssembly",
            "namespace Target; public sealed class TargetOnly { }");
        var mapping = new ExternalSourceMapping(
            "https://gitea.example/shared.git",
            "src/Shared.slnx",
            ["TargetAssembly"]);
        using var snapshot = ExternalSourceSnapshotTestFactory.CreateSnapshot(
            temp.DirectoryPath,
            mapping,
            new ExternalSourceProjectSpec(
                "SourceProject",
                "TargetAssembly",
                """
                namespace Source;
                public sealed class SourceOnly
                {
                    public void SelectedMember() { }
                    public void UnselectedMember() { }
                }

                public static class SourceExtensions
                {
                    public static string SourceOnlyExtension(this string value) => value;
                    public static string SourceOnlySecondaryExtension(this string value) => value;
                }
                """));
        var settingsPath = CreateSettings(temp);
        var providerDiagnostic = new ExternalSourceConfigurationDiagnostic(
            "provider-diagnostic",
            "Kontrollierte Providerdiagnose",
            "warning",
            "test-provider");
        var provider = new RecordingProvider(new ExternalSourceProviderResult(
            isAvailable: true,
            diagnostics: [providerDiagnostic],
            sourceSnapshot: snapshot));
        using var composition = AssemblyAnalysisHostComposition.Create(settingsPath, provider);
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        var hostRegistry = composition.Registry;

        for (var session = 0; session < 2; session++)
        {
            var results = await RunAssemblySessionAsync(assemblyPath, registry, composition);

            AssertSourceBackedInspection(results.Inspect, providerDiagnostic);
            AssertSourceBackedExtensions(results.Extensions, providerDiagnostic);
            Assert.False(composition.IsDisposed);
            Assert.Same(hostRegistry, composition.Registry);
            Assert.Equal(1, composition.Registry.ResidentCount);
            Assert.False(snapshot.IsDisposed);
        }

        Assert.Equal(4, provider.CallCount);
        Assert.NotNull(provider.FirstMapping);
        Assert.All(provider.Mappings, observed => Assert.Same(provider.FirstMapping, observed));
        Assert.All(provider.Mappings, observed => Assert.Equal("TargetAssembly", observed.Assemblies.Single()));
        Assert.Equal(4, provider.CancellationTokens.Count);
        Assert.All(provider.CancellationTokens, token => Assert.True(token.CanBeCanceled));

        composition.Dispose();
        composition.Dispose();

        Assert.True(composition.IsDisposed);
        Assert.Equal(0, composition.Registry.ResidentCount);
        Assert.True(snapshot.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = composition.Orchestrator);
    }

    private static string CreateSettings(TestTempDirectory temp)
    {
        temp.CreateFile(
            "mappings.json",
            "{ \"repositories\": [{ \"url\": \"https://gitea.example/shared.git\", \"solutionPath\": \"src/Shared.slnx\", \"assemblies\": [\"TargetAssembly\"] }] }");
        return temp.CreateFile(
            "appsettings.json",
            "{ \"ExternalSources\": { \"MappingsPath\": \"mappings.json\" } }");
    }

    private static async Task<(CallToolResult Inspect, CallToolResult Extensions)> RunAssemblySessionAsync(
        string assemblyPath,
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition composition)
    {
        var (clientStream, daemonStream) = ThinClientPipeTestDoubles.CreateDuplexPair();
        await using var clientConnection = clientStream;
        await using var daemonConnection = new DaemonPipeConnection(daemonStream);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var serverTask = CreateSession(registry, composition).RunAsync(daemonConnection);
        CallToolResult inspect;
        CallToolResult extensions;
        try
        {
            await using (var client = await McpClient.CreateAsync(
                new StreamClientTransport(clientStream, clientStream),
                cancellationToken: timeout.Token).ConfigureAwait(false))
            {
                var tools = await client.ListToolsAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
                Assert.Contains(tools, tool => tool.Name == "inspect_assembly");
                Assert.Contains(tools, tool => tool.Name == "find_assembly_extensions");

                inspect = await client.CallToolAsync(
                    "inspect_assembly",
                    new Dictionary<string, object?>
                    {
                        ["targetType"] = "assembly",
                        ["targetPath"] = assemblyPath,
                        ["typeName"] = "SourceOnly",
                        ["exactTypeName"] = true,
                        ["memberName"] = "Member",
                        ["maxResults"] = 1,
                        ["maxMembers"] = 1,
                    },
                    cancellationToken: timeout.Token).ConfigureAwait(false);
                extensions = await client.CallToolAsync(
                    "find_assembly_extensions",
                    new Dictionary<string, object?>
                    {
                        ["targetType"] = "assembly",
                        ["targetPath"] = assemblyPath,
                        ["extensionName"] = "SourceOnly",
                        ["namespace"] = "Source",
                        ["maxResults"] = 1,
                    },
                    cancellationToken: timeout.Token).ConfigureAwait(false);
            }
        }
        finally
        {
            await clientConnection.DisposeAsync().ConfigureAwait(false);
            await daemonConnection.DisposeAsync().ConfigureAwait(false);
        }

        await serverTask.WaitAsync(timeout.Token).ConfigureAwait(false);
        return (inspect, extensions);
    }

    private static DaemonMcpSession CreateSession(
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition composition) =>
        new(
            runtimeContext => McpServerOptionsFactory.BuildToolCollection(
                registry,
                runtimeContext,
                composition),
            () => McpServerOptionsFactory.BuildResourceCollection(registry));

    private static void AssertSourceBackedInspection(
        CallToolResult result,
        ExternalSourceConfigurationDiagnostic providerDiagnostic)
    {
        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Herkunft: `source-backed`", text, StringComparison.Ordinal);
        Assert.Contains("SourceOnly", text, StringComparison.Ordinal);
        Assert.Contains(providerDiagnostic.Code, text, StringComparison.Ordinal);
        Assert.Contains(providerDiagnostic.Message, text, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetOnly", text, StringComparison.Ordinal);
        Assert.DoesNotContain("decompiled", text, StringComparison.Ordinal);

        var payload = StructuredOf(result);
        Assert.Equal("source-backed", payload.GetProperty("origin").GetProperty("originKind").GetString());
        var type = Assert.Single(payload.GetProperty("types").EnumerateArray());
        Assert.Equal("SourceOnly", type.GetProperty("name").GetString());
        Assert.Equal(1, type.GetProperty("members").GetArrayLength());
        Assert.Equal(2, type.GetProperty("totalMembers").GetInt32());
        Assert.True(type.GetProperty("membersTruncated").GetBoolean());
        Assert.Equal("SelectedMember", type.GetProperty("members")[0].GetProperty("name").GetString());
    }

    private static void AssertSourceBackedExtensions(
        CallToolResult result,
        ExternalSourceConfigurationDiagnostic providerDiagnostic)
    {
        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Herkunft: `source-backed`", text, StringComparison.Ordinal);
        Assert.Contains("SourceOnlyExtension", text, StringComparison.Ordinal);
        Assert.Contains(providerDiagnostic.Code, text, StringComparison.Ordinal);
        Assert.Contains(providerDiagnostic.Message, text, StringComparison.Ordinal);
        Assert.DoesNotContain("decompiled", text, StringComparison.Ordinal);

        var payload = StructuredOf(result);
        Assert.Equal("source-backed", payload.GetProperty("origin").GetProperty("originKind").GetString());
        Assert.Equal(2, payload.GetProperty("totalExtensions").GetInt32());
        Assert.True(payload.GetProperty("truncated").GetBoolean());
        var extension = Assert.Single(payload.GetProperty("extensions").EnumerateArray());
        Assert.Equal("SourceOnlyExtension", extension.GetProperty("name").GetString());
        Assert.Equal("Source", extension.GetProperty("namespace").GetString());
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private static JsonElement StructuredOf(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value;
    }

    private sealed class RecordingProvider : IExternalSourceProvider
    {
        private readonly ExternalSourceProviderResult result;

        internal RecordingProvider(ExternalSourceProviderResult result) => this.result = result;

        internal int CallCount { get; private set; }

        internal List<ExternalSourceMapping> Mappings { get; } = [];

        internal List<CancellationToken> CancellationTokens { get; } = [];

        internal ExternalSourceMapping? FirstMapping => Mappings.FirstOrDefault();

        public ValueTask<ExternalSourceProviderResult> ResolveAsync(
            ExternalSourceMapping mapping,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Mappings.Add(mapping);
            CancellationTokens.Add(cancellationToken);
            return ValueTask.FromResult(result);
        }
    }
}
