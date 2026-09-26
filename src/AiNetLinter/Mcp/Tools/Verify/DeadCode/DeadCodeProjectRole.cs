#nullable enable

using System.Linq;
using AiNetLinter.Configuration;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Mcp.Tools.Verify.DeadCode;

internal static class DeadCodeProjectRole
{
    internal static string Resolve(Project project, Config? config)
    {
        if (config?.DeadCode.ProjectRoles.TryGetValue(project.Name, out var role) == true)
            return role is "production" or "test" ? role : "unknown";
        if (project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.IsTestProject", out var isTest))
            return bool.TryParse(isTest, out var value) ? value ? "test" : "production" : "unknown";
        return project.MetadataReferences.Any(reference => System.IO.Path.GetFileNameWithoutExtension(reference.Display) is
            "xunit.core" or "xunit.v3.core" or "nunit.framework" or "Microsoft.VisualStudio.TestPlatform.TestFramework")
            ? "test" : "production";
    }
}
