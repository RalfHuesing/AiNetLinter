#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using Xunit;

namespace AiNetLinter.Tests.Core;

public sealed class WpfCodeBehindTests
{
    private static Config CreateConfig(
        bool wpfRequireMinimalCodeBehind = true,
        string[]? excludeClassNames = null)
    {
        return new Config
        {
            Global = new GlobalConfig
            {
                EnforceSealedClasses = false,
                AllowUnsealedPartialClasses = true,
                AllowDynamic = false,
                AllowOutParameters = false,
                EnforceValueObjectContracts = false,
                EnforcePascalCase = false,
                EnforceXmlDocumentation = false,
                EnforceSemanticNaming = false,
                EnforceNullableEnable = false,
                EnforceNoSilentCatch = false,
                EnforceResultPatternOverExceptions = false,
                EnforceExplicitStateImmutability = false,                PreventContextDependentOverloads = false,                EnforceNamespaceDirectoryMapping = false,
                DetectAndBanPhantomDependencies = false,
                EnableTestSentinel = false,
            },
            Metrics = new MetricsConfig(),
            UiSeparation = new UiSeparationConfig
            {
                WpfRequireMinimalCodeBehind = wpfRequireMinimalCodeBehind,
                WpfCodeBehindBaseTypes = new[] { "Window", "UserControl", "Page", "NavigationWindow" },
                WpfExcludeClassNames = excludeClassNames ?? Array.Empty<string>(),
                BlazorRequireCodeBehind = false,
                BlazorRequireCssIsolation = false,
            },
        };
    }

    private static SemanticModel GetSemanticModel(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var mscorlib = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        var compilation = CSharpCompilation.Create("WpfTestAssembly")
            .AddSyntaxTrees(tree)
            .AddReferences(mscorlib)
            .WithOptions(new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        return compilation.GetSemanticModel(tree);
    }

    [Fact]
    public void WpfCodeBehind_OnlyConstructor_NoViolation()
    {
        const string source = @"
public class Window { }
public partial class MainWindow : Window
{
    public MainWindow()
    {
        // InitializeComponent();
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MainWindow.xaml.cs", model, CreateConfig());

        Assert.DoesNotContain(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind");
    }

    [Fact]
    public void WpfCodeBehind_WithEventHandlerMethod_ReportsViolation()
    {
        const string source = @"
public class Window { }
public partial class MainWindow : Window
{
    public MainWindow()
    {
        // InitializeComponent();
    }

    private void Button_Click(object sender, object e)
    {
        // logic here
    }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MainWindow.xaml.cs", model, CreateConfig());

        Assert.Contains(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind"
            && v.Details.Contains("MainWindow"));
    }

    [Fact]
    public void WpfCodeBehind_WithProperty_ReportsViolation()
    {
        const string source = @"
public class UserControl { }
public partial class MyControl : UserControl
{
    public MyControl() { }
    public string Title { get; set; } = string.Empty;
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MyControl.xaml.cs", model, CreateConfig());

        Assert.Contains(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind"
            && v.Details.Contains("MyControl"));
    }

    [Fact]
    public void WpfCodeBehind_NonPartialClass_NoViolation()
    {
        const string source = @"
public class Window { }
public class MainWindow : Window
{
    public MainWindow() { }
    private void Button_Click(object sender, object e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MainWindow.cs", model, CreateConfig());

        Assert.DoesNotContain(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind");
    }

    [Fact]
    public void WpfCodeBehind_RuleDisabled_NoViolation()
    {
        const string source = @"
public class Window { }
public partial class MainWindow : Window
{
    public MainWindow() { }
    private void Button_Click(object sender, object e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MainWindow.xaml.cs", model, CreateConfig(wpfRequireMinimalCodeBehind: false));

        Assert.DoesNotContain(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind");
    }

    [Fact]
    public void WpfCodeBehind_ClassInExcludeList_NoViolation()
    {
        const string source = @"
public class Window { }
public partial class MainWindow : Window
{
    public MainWindow() { }
    private void Button_Click(object sender, object e) { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("MainWindow.xaml.cs", model,
            CreateConfig(excludeClassNames: new[] { "MainWindow" }));

        Assert.DoesNotContain(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind");
    }

    [Fact]
    public void WpfCodeBehind_UnknownBaseType_NoViolation()
    {
        const string source = @"
public class SomeOtherBase { }
public partial class SomeView : SomeOtherBase
{
    public SomeView() { }
    private void DoStuff() { }
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("SomeView.cs", model, CreateConfig());

        Assert.DoesNotContain(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind");
    }

    [Fact]
    public void WpfCodeBehind_NavigationWindowWithExtraMembers_ReportsViolation()
    {
        const string source = @"
public class NavigationWindow { }
public partial class AppShell : NavigationWindow
{
    public AppShell() { }
    private string _title = string.Empty;
}";
        var model = GetSemanticModel(source);
        var violations = LinterAnalyzer.Analyze("AppShell.xaml.cs", model, CreateConfig());

        Assert.Contains(violations, v => v.RuleName == "WpfRequireMinimalCodeBehind"
            && v.Details.Contains("AppShell"));
    }
}
