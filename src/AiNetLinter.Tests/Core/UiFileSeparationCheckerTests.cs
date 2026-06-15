#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class UiFileSeparationCheckerTests : IDisposable
{
    private readonly string _tempDir;

    public UiFileSeparationCheckerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AiNetLinterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static UiSeparationConfig AllEnabled() => new UiSeparationConfig
    {
        BlazorRequireCodeBehind = true,
        BlazorRequireCssIsolation = true,
        WpfRequireMinimalCodeBehind = true,
        WpfCodeBehindBaseTypes = new[] { "Window", "UserControl", "Page", "NavigationWindow" },
        BlazorExcludeFileNames = new[] { "_Imports.razor" },
        WpfExcludeClassNames = Array.Empty<string>(),
    };

    // ── IsRazorSuppressed ─────────────────────────────────────────────────────

    [Fact]
    public void IsRazorSuppressed_WithMatchingRuleName_ReturnsTrue()
    {
        const string content = "@* ainetlinter-disable BlazorRequireCodeBehind *@";
        Assert.True(UiFileSeparationChecker.IsRazorSuppressed(content, "BlazorRequireCodeBehind"));
    }

    [Fact]
    public void IsRazorSuppressed_WithDisableAll_ReturnsTrue()
    {
        const string content = "@* ainetlinter-disable all *@";
        Assert.True(UiFileSeparationChecker.IsRazorSuppressed(content, "BlazorRequireCodeBehind"));
    }

    [Fact]
    public void IsRazorSuppressed_WithDifferentRuleName_ReturnsFalse()
    {
        const string content = "@* ainetlinter-disable BlazorRequireCssIsolation *@";
        Assert.False(UiFileSeparationChecker.IsRazorSuppressed(content, "BlazorRequireCodeBehind"));
    }

    [Fact]
    public void IsRazorSuppressed_WithEmptyContent_ReturnsFalse()
    {
        Assert.False(UiFileSeparationChecker.IsRazorSuppressed(string.Empty, "BlazorRequireCodeBehind"));
    }

    [Fact]
    public void IsRazorSuppressed_CSharpStyleComment_ReturnsTrue()
    {
        const string content = "// ainetlinter-disable BlazorRequireCodeBehind";
        Assert.True(UiFileSeparationChecker.IsRazorSuppressed(content, "BlazorRequireCodeBehind"));
    }

    // ── ScanDirectory — BlazorRequireCodeBehind ───────────────────────────────

    [Fact]
    public void ScanDirectory_RazorWithoutCodeBehind_ReportsViolation()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), "<h1>Hello</h1>");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Contains(violations, v => v.RuleName == "BlazorRequireCodeBehind" && v.Details.Contains("MyComponent.razor"));
    }

    [Fact]
    public void ScanDirectory_RazorWithCodeBehind_NoCodeBehindViolation()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), "<h1>Hello</h1>");
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor.cs"), "public partial class MyComponent {}");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.DoesNotContain(violations, v => v.RuleName == "BlazorRequireCodeBehind");
    }

    // ── ScanDirectory — BlazorRequireCssIsolation ─────────────────────────────

    [Fact]
    public void ScanDirectory_RazorWithoutCss_ReportsCssViolation()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), "<h1>Hello</h1>");
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor.cs"), "public partial class MyComponent {}");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Contains(violations, v => v.RuleName == "BlazorRequireCssIsolation" && v.Details.Contains("MyComponent.razor"));
    }

    [Fact]
    public void ScanDirectory_RazorWithCss_NoCssViolation()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), "<h1>Hello</h1>");
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor.cs"), "public partial class MyComponent {}");
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor.css"), "h1 { color: red; }");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.DoesNotContain(violations, v => v.RuleName == "BlazorRequireCssIsolation");
    }

    // ── ScanDirectory — Exclusions ─────────────────────────────────────────────

    [Fact]
    public void ScanDirectory_ImportsRazor_IsExcluded()
    {
        File.WriteAllText(Path.Combine(_tempDir, "_Imports.razor"), "@using System");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Empty(violations);
    }

    [Fact]
    public void ScanDirectory_RazorInObjDirectory_IsExcluded()
    {
        var objDir = Path.Combine(_tempDir, "obj", "Debug");
        Directory.CreateDirectory(objDir);
        File.WriteAllText(Path.Combine(objDir, "MyComponent.razor"), "<h1>Hello</h1>");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Empty(violations);
    }

    // ── ScanDirectory — Suppression ────────────────────────────────────────────

    [Fact]
    public void ScanDirectory_WithCodeBehindSuppression_NoCodeBehindViolation()
    {
        const string content = "@* ainetlinter-disable BlazorRequireCodeBehind *@\n<h1>Hello</h1>";
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), content);

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.DoesNotContain(violations, v => v.RuleName == "BlazorRequireCodeBehind");
    }

    [Fact]
    public void ScanDirectory_WithDisableAll_NoViolations()
    {
        const string content = "@* ainetlinter-disable all *@\n<h1>Hello</h1>";
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), content);

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Empty(violations);
    }

    // ── ScanDirectory — NonExistent directory ─────────────────────────────────

    [Fact]
    public void ScanDirectory_NonExistentDirectory_DoesNotThrow()
    {
        var violations = new ConcurrentBag<RuleViolation>();
        var ex = Record.Exception(() =>
            UiFileSeparationChecker.ScanDirectory(Path.Combine(_tempDir, "doesnotexist"), violations, AllEnabled()));
        Assert.Null(ex);
    }
}
