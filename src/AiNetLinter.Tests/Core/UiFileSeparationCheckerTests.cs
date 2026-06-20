#nullable enable

using System;
using System.Collections.Concurrent;
using System.IO;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Core.Checkers;
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
        BlazorCssIsolationOnlyWhenStylesNeeded = true,
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
    public void ScanDirectory_RazorWithInlineCodeButNoCodeBehind_ReportsViolation()
    {
        const string content = "<h1>Hello</h1>\n@code { private int _count = 0; }";
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), content);

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Contains(violations, v => v.RuleName == "BlazorRequireCodeBehind" && v.Details.Contains("MyComponent.razor"));
    }

    [Fact]
    public void ScanDirectory_RazorWithoutInlineCodeAndWithoutCodeBehind_NoViolation()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyComponent.razor"), "<MudButton>Click</MudButton>");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.DoesNotContain(violations, v => v.RuleName == "BlazorRequireCodeBehind");
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
        const string content = "@* ainetlinter-disable BlazorRequireCodeBehind *@\n<h1>Hello</h1>\n@code { private int _x = 0; }";
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

    // ── RazorNeedsCss ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("<div class=\"foo\">")]
    [InlineData("<span>Text</span>")]
    [InlineData("<p>Paragraph</p>")]
    [InlineData("<input type=\"text\" />")]
    [InlineData("<h1>Title</h1>")]
    [InlineData("<ul><li>Item</li></ul>")]
    [InlineData("class=\"my-class\"")]
    [InlineData("class='my-class'")]
    [InlineData("class=@myClass")]
    [InlineData("style=\"color: red\"")]
    [InlineData("style='color: red'")]
    [InlineData("style=@myStyle")]
    public void RazorNeedsCss_WithHtmlOrStyleContent_ReturnsTrue(string content)
    {
        Assert.True(UiFileSeparationChecker.RazorNeedsCss(content));
    }

    [Theory]
    [InlineData("<MudDialog>")]
    [InlineData("<MudButton Color=\"Color.Primary\">Click</MudButton>")]
    [InlineData("<DynamicFormFieldEditor Host=\"@Host\" />")]
    [InlineData("@namespace MyApp.Components")]
    [InlineData("@using System")]
    [InlineData("@* Razor comment *@")]
    [InlineData("<TitleContent><MudText>Title</MudText></TitleContent>")]
    [InlineData("")]
    public void RazorNeedsCss_WithPureBlazoOrDirectives_ReturnsFalse(string content)
    {
        Assert.False(UiFileSeparationChecker.RazorNeedsCss(content));
    }

    [Fact]
    public void RazorNeedsCss_ExampleFromBugReport_ReturnsFalse()
    {
        const string content = """
            @namespace San.smart.Planner.Platform.Components.UI.Form
            @using San.smart.Planner.Platform.Handlers

            <MudDialog>
                <TitleContent>
                    <MudText Typo="Typo.h6">@Title</MudText>
                </TitleContent>
                <DialogContent>
                    <DynamicFormFieldEditor Host="@Host"
                                            Fields="@Fields"
                                            Context="@Values" />
                </DialogContent>
                <DialogActions>
                    <MudButton Variant="Variant.Outlined" Size="Size.Small" OnClick="Cancel">Abbrechen</MudButton>
                    <MudButton Color="Color.Primary" Variant="Variant.Filled" Size="Size.Small" OnClick="Save">Übernehmen</MudButton>
                </DialogActions>
            </MudDialog>
            """;

        Assert.False(UiFileSeparationChecker.RazorNeedsCss(content));
    }

    [Fact]
    public void ScanDirectory_PureComponentComposition_NoCssViolation()
    {
        const string content = "<MudDialog><MudButton>Click</MudButton></MudDialog>";
        File.WriteAllText(Path.Combine(_tempDir, "MyDialog.razor"), content);
        File.WriteAllText(Path.Combine(_tempDir, "MyDialog.razor.cs"), "public partial class MyDialog {}");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.DoesNotContain(violations, v => v.RuleName == "BlazorRequireCssIsolation");
    }

    [Fact]
    public void ScanDirectory_WithHtmlElements_CssViolationReported()
    {
        const string content = "<div class=\"container\"><MudButton>Click</MudButton></div>";
        File.WriteAllText(Path.Combine(_tempDir, "MyPage.razor"), content);
        File.WriteAllText(Path.Combine(_tempDir, "MyPage.razor.cs"), "public partial class MyPage {}");

        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, AllEnabled());

        Assert.Contains(violations, v => v.RuleName == "BlazorRequireCssIsolation");
    }

    // ── RazorHasInlineCode ────────────────────────────────────────────────────

    [Theory]
    [InlineData("@code { private int _x = 0; }")]
    [InlineData("<h1>Hello</h1>\n@code\n{\n    private void Foo() {}\n}")]
    [InlineData("@functions { private string Bar() => string.Empty; }")]
    public void RazorHasInlineCode_WithCodeBlock_ReturnsTrue(string content)
    {
        Assert.True(UiFileSeparationChecker.RazorHasInlineCode(content));
    }

    [Theory]
    [InlineData("<MudButton>Click</MudButton>")]
    [InlineData("<h1>Hello</h1>")]
    [InlineData("@namespace MyApp\n@using System\n<MudDialog />")]
    [InlineData("")]
    public void RazorHasInlineCode_WithoutCodeBlock_ReturnsFalse(string content)
    {
        Assert.False(UiFileSeparationChecker.RazorHasInlineCode(content));
    }

    [Fact]
    public void ScanDirectory_WhenOnlyWhenNeededFalse_AlwaysReportsCssViolation()
    {
        const string content = "<MudDialog><MudButton>Click</MudButton></MudDialog>";
        File.WriteAllText(Path.Combine(_tempDir, "MyDialog.razor"), content);
        File.WriteAllText(Path.Combine(_tempDir, "MyDialog.razor.cs"), "public partial class MyDialog {}");

        var config = AllEnabled() with { BlazorCssIsolationOnlyWhenStylesNeeded = false };
        var violations = new ConcurrentBag<RuleViolation>();
        UiFileSeparationChecker.ScanDirectory(_tempDir, violations, config);

        Assert.Contains(violations, v => v.RuleName == "BlazorRequireCssIsolation");
    }
}
