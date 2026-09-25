#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace AiNetLinter.IntegrationTests.Mcp.Tools.Verify;

internal static class VerifyAdvisoryTestData
{
    public static string ManyUnusedMembersSource(
        int count,
        string typeName = "ManyUnusedMembers",
        string namespaceName = "BaselineMini")
    {
        var members = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, count).Select(index => $"    private static void UnusedCandidate{index:D2}() {{ }}"));
        return $$"""
            namespace {{namespaceName}};

            public sealed class {{typeName}}
            {
            {{members}}
            }
            """;
    }

    public static IEnumerable<(string RelativePath, string Content)> ManyUnusedMemberFiles()
    {
        for (var fileIndex = 0; fileIndex < 48; fileIndex++)
        {
            var feature = $"AuthorizationFeature{fileIndex:D2}";
            var directory = (fileIndex % 4) switch
            {
                0 => "Features/Identity/Policies",
                1 => "Features/Payments/Validation",
                2 => "Features/Notifications/Delivery",
                _ => "Features/Reporting/Exports",
            };
            var relativePath = $"src/BaselineMini/{directory}/{feature}ReviewPolicy.cs";
            var namespaceName = $"BaselineMini.{directory.Replace('/', '.')}";
            yield return (relativePath, ManyUnusedMembersSource(15, $"{feature}ReviewPolicy", namespaceName));
        }
    }
}
