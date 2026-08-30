#nullable enable

using AiNetLinter.Mcp.Projects;
using AiNetLinter.Mcp.Assemblies.Analysis;
using AiNetLinter.Mcp.Registration;
using ModelContextProtocol.Server;

namespace AiNetLinter.Mcp.Composition;

internal static class McpServerToolCollectionFactory
{
    internal static McpServerPrimitiveCollection<McpServerTool> Build(
        ProjectRegistry registry,
        AnalysisToolRoute targetRoute,
        Daemon.DaemonRuntimeContext? runtimeContext = null,
        IAssemblyAnalysisRegistry? assemblyRegistry = null)
    {
        var tools = new McpServerPrimitiveCollection<McpServerTool>();
        SymbolGraphToolRegistrations.Register(tools, targetRoute);
        AssemblyAnalysisToolRegistrations.Register(tools, targetRoute);
        FileStructureToolRegistrations.Register(tools, registry, targetRoute);
        AnalysisToolRegistrations.Register(tools, registry, targetRoute);
        SymbolBodyToolRegistrations.Register(tools, targetRoute);
        ServerMaintenanceToolRegistrations.Register(tools, registry, runtimeContext, assemblyRegistry);
        DuplicateDetectionToolRegistrations.Register(tools, registry);
        return tools;
    }
}
