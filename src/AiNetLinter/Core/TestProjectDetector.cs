#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AiNetLinter.Output;
using Microsoft.CodeAnalysis;

namespace AiNetLinter.Core;

/// <summary>
/// Hilfsklasse zur Erkennung von Testprojekten.
/// Delegiert an die zentrale <see cref="TestDetector"/>-Klasse.
/// </summary>
public static class TestProjectDetector
{
    /// <summary>
    /// Prüft, ob ein Projekt ein Testprojekt ist.
    /// Delegiert an <see cref="TestDetector.IsTestProject"/>.
    /// </summary>
    public static bool IsTestProject(Project project, IReadOnlyList<string>? testProjectNameSuffixes = null)
        => TestDetector.IsTestProject(project, testProjectNameSuffixes);
}
