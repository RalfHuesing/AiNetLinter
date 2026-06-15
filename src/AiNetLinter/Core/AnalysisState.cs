#nullable enable

using System;
using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using AiNetLinter.Models;
using AiNetLinter.Metrics;

namespace AiNetLinter.Core;

/// <summary>
/// Hält den aktuellen Zustand während der parallelen Dateianalyse.
/// </summary>
internal sealed record AnalysisState(
    Solution Solution,
    ConcurrentBag<RuleViolation> Violations,
    TestCoverageIndex TestCoverage,
    ConcurrentBag<ClassInfo> SourceClasses,
    ConcurrentBag<PartialClassPart> PartialClassParts,
    ConcurrentDictionary<string, string> FileContents
);
