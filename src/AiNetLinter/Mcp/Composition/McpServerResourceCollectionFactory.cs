#nullable enable

using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Registration;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Composition;

internal static class McpServerResourceCollectionFactory
{
    internal static McpServerResourceCollection Build(ProjectRegistry registry)
    {
        var resources = new McpServerResourceCollection();
        McpAgentGuideRegistration.Register(resources);
        OverviewResourceRegistration.Register(resources, registry);
        RulesResourceRegistration.Register(resources, registry);
        return resources;
    }
}
