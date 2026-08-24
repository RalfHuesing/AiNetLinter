#nullable enable

namespace AiNetLinter.Logging;

/// <summary>Aktivierungs-Rollen des Prozesses fuer das ProcessRole-Feld im Log.</summary>
internal static class ProcessRoles
{
    public const string Cli = "cli";
    public const string ThinClient = "thin-client";
    public const string Daemon = "daemon";
    public const string McpServer = "mcp-server";
}
