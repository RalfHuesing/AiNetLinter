#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Configuration;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Assemblies;
using AiNetLinter.Mcp.Composition;
using AiNetLinter.Mcp.Daemon;
using AiNetLinter.Mcp.Projects;
using AiNetLinter.Output;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Daemon;

// @covers DaemonMcpSession
[Trait("Category", "Integration")]
public sealed class DaemonHostMcpContractTests
{
    [Fact]
    public async Task RunMcpSessionAsync_UsesTheExistingMcpSessionRunnerOnConnectionEof()
    {
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        await using var connection = new DaemonPipeConnection(new MemoryStream());
        await using var composition = AssemblyAnalysisHostComposition.Create();

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
            """
            namespace Target;
            public sealed class TargetOnly
            {
                public void SelectedMember() { }
                public void UnselectedMember() { }
            }
            public static class TargetExtensions
            {
                public static string TargetOnlyExtension(this string value) => value;
            }
            """);
        await using var composition = AssemblyAnalysisHostComposition.Create();
        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        var hostRegistry = composition.Sessions;

        for (var session = 0; session < 2; session++)
        {
            var results = await RunAssemblySessionAsync(assemblyPath, registry, composition);

            AssertDecompiledInspection(results.Inspect);
            AssertDecompiledExtensions(results.Extensions);
            Assert.False(composition.IsDisposed);
            Assert.Same(hostRegistry, composition.Sessions);
            Assert.Equal(1, composition.Sessions.ResidentCount);
        }

        await composition.DisposeAsync();
        await composition.DisposeAsync();

        Assert.True(composition.IsDisposed);
        Assert.Equal(0, hostRegistry.ResidentCount);
    }

    [Fact]
    public async Task RunMcpSessionAsync_RegisteredInspectAssemblyPreservesNativePeRecoverability()
    {
        var nativeAssemblyPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "kernel32.dll");
        Assert.True(File.Exists(nativeAssemblyPath), $"Native PE fixture fehlt: {nativeAssemblyPath}");

        await using var registry = ProjectWiringFixtures.CreateLoadedRegistry(TimeProvider.System);
        await using var composition = AssemblyAnalysisHostComposition.Create();
        var result = await RunRegisteredAssemblyToolAsync(
            "inspect_assembly",
            nativeAssemblyPath,
            registry,
            composition);

        Assert.False(result.IsError);
        var payload = StructuredOf(result);
        Assert.Equal(LinterErrorCodes.WorkspaceDiagnostic, payload.GetProperty("code").GetString());
        Assert.Equal(nativeAssemblyPath, payload.GetProperty("context").GetString());
        Assert.Contains(".dll oder .exe mit IL", payload.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            McpToolResults.NativePeAssemblyHint,
            payload.GetProperty("hint").GetString());
        Assert.True(payload.GetProperty("recoverable").GetBoolean());
    }

    private static async Task<(CallToolResult Inspect, CallToolResult Extensions)> RunAssemblySessionAsync(
        string assemblyPath,
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition composition)
    {
        var (clientStream, daemonStream) = ThinClientPipeTestDoubles.CreateDuplexPair();
        await using var clientConnection = clientStream;
        await using var daemonConnection = new DaemonPipeConnection(daemonStream);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(180));
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
                        ["typeName"] = "TargetOnly",
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
                        ["extensionName"] = "TargetOnly",
                        ["namespace"] = "Target",
                        ["maxResults"] = 1,
                        ["includeReferences"] = false,
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

    private static async Task<CallToolResult> RunRegisteredAssemblyToolAsync(
        string toolName,
        string assemblyPath,
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition composition)
    {
        var (clientStream, daemonStream) = ThinClientPipeTestDoubles.CreateDuplexPair();
        await using var clientConnection = clientStream;
        await using var daemonConnection = new DaemonPipeConnection(daemonStream);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var serverTask = CreateSession(registry, composition).RunAsync(daemonConnection);
        CallToolResult result = null!;
        try
        {
            await using var client = await McpClient.CreateAsync(
                new StreamClientTransport(clientStream, clientStream),
                cancellationToken: timeout.Token).ConfigureAwait(false);
            var tools = await client.ListToolsAsync(cancellationToken: timeout.Token).ConfigureAwait(false);
            Assert.Contains(tools, tool => tool.Name == toolName);
            result = await client.CallToolAsync(
                toolName,
                new Dictionary<string, object?>
                {
                    ["targetType"] = "assembly",
                    ["targetPath"] = assemblyPath,
                },
                cancellationToken: timeout.Token).ConfigureAwait(false);
        }
        finally
        {
            await clientConnection.DisposeAsync().ConfigureAwait(false);
            await daemonConnection.DisposeAsync().ConfigureAwait(false);
            await serverTask.WaitAsync(timeout.Token).ConfigureAwait(false);
        }

        return result;
    }

    private static DaemonMcpSession CreateSession(
        ProjectRegistry registry,
        AssemblyAnalysisHostComposition composition) =>
        new(
            runtimeContext => McpServerToolCollectionFactory.Build(
                registry,
                AnalysisToolCall.CreateTargetRoute(
                    ProjectAnalysisDispatcher.CreateRoute(registry),
                    AssemblyAnalysisDispatcher.CreateRoute(composition.Sessions)),
                runtimeContext),
            () => McpServerResourceCollectionFactory.Build(registry));

    private static void AssertDecompiledInspection(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Quelle: Dekompilat", text, StringComparison.Ordinal);
        Assert.Contains("TargetOnly", text, StringComparison.Ordinal);

        var payload = StructuredOf(result);
        Assert.Equal("decompiled", payload.GetProperty("origin").GetProperty("originKind").GetString());
        var type = Assert.Single(payload.GetProperty("types").EnumerateArray());
        Assert.Equal("TargetOnly", type.GetProperty("name").GetString());
        Assert.Equal(1, type.GetProperty("members").GetArrayLength());
        Assert.Equal(2, type.GetProperty("totalMembers").GetInt32());
        Assert.True(type.GetProperty("membersTruncated").GetBoolean());
        Assert.Equal("SelectedMember", type.GetProperty("members")[0].GetProperty("name").GetString());
    }

    private static void AssertDecompiledExtensions(CallToolResult result)
    {
        Assert.NotEqual(true, result.IsError);
        var text = TextOf(result);
        Assert.Contains("Quelle: Dekompilat", text, StringComparison.Ordinal);
        Assert.Contains("TargetOnlyExtension", text, StringComparison.Ordinal);

        var payload = StructuredOf(result);
        Assert.Equal("decompiled", payload.GetProperty("origin").GetProperty("originKind").GetString());
        Assert.Equal(1, payload.GetProperty("totalExtensions").GetInt32());
        var extension = Assert.Single(payload.GetProperty("extensions").EnumerateArray());
        Assert.Equal("TargetOnlyExtension", extension.GetProperty("name").GetString());
        Assert.Equal("Target", extension.GetProperty("namespace").GetString());
    }

    private static string TextOf(CallToolResult result) =>
        Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;

    private static JsonElement StructuredOf(CallToolResult result)
    {
        Assert.NotNull(result.StructuredContent);
        return result.StructuredContent!.Value;
    }
}
