#nullable enable

namespace AiNetLinter.Mcp.Tools.Common;

internal static class TestEvidencePriorities
{
    internal static int For(string evidenceKind) => evidenceKind switch
    {
        "directInvocation" => 0,
        "explicitMemberCoverage" => 1,
        "memberNameMatch" => 2,
        "directTypeUse" => 3,
        "explicitTypeCoverage" => 4,
        "typeNamingConvention" => 5,
        _ => 6,
    };
}
