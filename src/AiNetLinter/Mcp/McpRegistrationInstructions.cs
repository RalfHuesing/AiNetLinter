#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AiNetLinter.Mcp;

internal static class McpRegistrationInstructions
{
    private const string DefaultCommand = "ainetlinter";
    private const string McpServerArgument = "--mcp-server";

    internal static string AppendRuntimeBlock(string documentation)
    {
        ArgumentNullException.ThrowIfNull(documentation);
        return documentation.TrimEnd() + "\n\n" + BuildRuntimeBlock();
    }

    internal static string BuildRuntimeBlock() =>
        BuildRuntimeBlock(Environment.ProcessPath, typeof(McpRegistrationInstructions).Assembly.Location);

    internal static string BuildRuntimeBlock(string? processPath, string? assemblyPath)
    {
        var launch = ResolveLaunch(processPath, assemblyPath);
        var status = launch.IsRuntimeResolved
            ? "Der Startpfad wurde zur Laufzeit aus dem aktuell laufenden Prozess ermittelt."
            : "Der aktuelle Startpfad konnte nicht vollständig ermittelt werden; stelle sicher, dass `ainetlinter` über PATH auflösbar ist.";

        var command = JsonSerializer.Serialize(launch.Command);
        var arguments = JsonSerializer.Serialize(launch.Arguments);
        var builder = new StringBuilder();
        builder.AppendLine("## Laufzeitpfad des MCP-Servers");
        builder.AppendLine();
        builder.AppendLine(status);
        builder.AppendLine("Für eine neue MCP-Host-Registrierung kann dieser Block verwendet werden:");
        builder.AppendLine();
        builder.AppendLine("```json");
        builder.AppendLine("{");
        builder.AppendLine($"  \"command\": {command},");
        builder.AppendLine($"  \"args\": {arguments}");
        builder.AppendLine("}");
        builder.AppendLine("```");
        return builder.ToString().TrimEnd();
    }

    internal static McpLaunchSpec ResolveLaunch(string? processPath, string? assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(processPath))
        {
            return new McpLaunchSpec(DefaultCommand, [McpServerArgument], IsRuntimeResolved: false);
        }

        if (IsDotnetHost(processPath))
        {
            return string.IsNullOrWhiteSpace(assemblyPath)
                ? new McpLaunchSpec(DefaultCommand, [McpServerArgument], IsRuntimeResolved: false)
                : new McpLaunchSpec(processPath, [assemblyPath, McpServerArgument], IsRuntimeResolved: true);
        }

        return new McpLaunchSpec(processPath, [McpServerArgument], IsRuntimeResolved: true);
    }

    private static bool IsDotnetHost(string processPath) =>
        Path.GetFileNameWithoutExtension(processPath)
            .Equals("dotnet", StringComparison.OrdinalIgnoreCase);

}
