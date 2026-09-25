#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiNetLinter.IntegrationTests.Mcp.Platform;
using ModelContextProtocol.Protocol;
using Xunit;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.Verify;

public sealed partial class VerifyToolContractE2ETests
{
    [Fact]
    public async Task GetVerifyAdvisories_InvalidCategory_ReturnsParameterError()
    {
        using var fixture = CreateGitFixture();
        await using var host = await McpProcessHost.StartAsync(fixture, TimeSpan.FromSeconds(60));

        var result = await host.CallToolAsync(
            "get_verify_advisories",
            new Dictionary<string, object?> { ["category"] = "magic_value" });

        AssertVerifyResult(result, expectedError: true, "INVALID_ARGUMENT", "category muss dead_code sein", "field: $.category");
    }

}
