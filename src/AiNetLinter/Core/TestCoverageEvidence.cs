#nullable enable

namespace AiNetLinter.Core;

/// <summary>
/// Rangfolge der statischen Belege fuer die Zuordnung eines Tests zu einem Ziel.
/// Die ersten drei Werte koennen eine konkrete Testmethode benennen; Typbelege
/// bleiben bewusst auf Datei-/Klassenebene.
/// </summary>
public enum TestEvidenceKind
{
    DirectInvocation = 0,
    ExplicitMemberCoverage = 1,
    MemberNameMatch = 2,
    DirectTypeUse = 3,
    ExplicitTypeCoverage = 4,
    TypeNamingConvention = 5,
}

public static class TestEvidenceKindNames
{
    public static string ToWire(TestEvidenceKind kind) => kind switch
    {
        TestEvidenceKind.DirectInvocation => "directInvocation",
        TestEvidenceKind.ExplicitMemberCoverage => "explicitMemberCoverage",
        TestEvidenceKind.MemberNameMatch => "memberNameMatch",
        TestEvidenceKind.DirectTypeUse => "directTypeUse",
        TestEvidenceKind.ExplicitTypeCoverage => "explicitTypeCoverage",
        TestEvidenceKind.TypeNamingConvention => "typeNamingConvention",
        _ => "unknown",
    };

    public static string ToConfidence(TestEvidenceKind kind) => kind switch
    {
        TestEvidenceKind.DirectInvocation => "high",
        TestEvidenceKind.ExplicitMemberCoverage => "high",
        TestEvidenceKind.MemberNameMatch => "medium",
        TestEvidenceKind.DirectTypeUse => "high",
        TestEvidenceKind.ExplicitTypeCoverage => "medium",
        TestEvidenceKind.TypeNamingConvention => "low",
        _ => "low",
    };
}
