#nullable enable

using System.Collections.Generic;

namespace AiNetLinter.Mcp.Tools.DuplicateDetection;

/// <summary>Ergebnis von <see cref="RefactoringDriftScanner.ScanAsync"/> ohne Transportprojektion.</summary>
internal sealed record RefactoringDriftScanResultForTool(
    string HelperSymbolDisplayName,
    IReadOnlyList<Core.DuplicateDetection.RefactoringDriftCandidate> ShownCandidates,
    int TotalCandidates,
    int MethodsScanned,
    bool Truncated);
