#nullable enable

using System.Collections.Concurrent;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Kontext für die Test-Sentinel-Überprüfung.
/// </summary>
internal sealed record TestSentinelContext(
    TestCoverageIndex TestCoverage,
    ConcurrentBag<RuleViolation> Violations,
    ConcurrentDictionary<string, string> FileContents
);
