#nullable enable

namespace AiNetLinter.TestKit;

/// <summary>
/// In-Memory-Spiegel der Disk-Fixture <c>tests/Fixtures/FilterMini/</c>: dieselbe Quelltextstruktur
/// (Produktions- und Testprojekt, drei Namespaces, public/private- und public/internal-Mix) als
/// <see cref="ProjectSpec"/>-Paar fuer <see cref="RoslynTestSolutionFactory.CreateSolution"/>.
/// </summary>
public static class FilterMiniSolutionSpec
{
    public static ProjectSpec[] CreateProjectSpecs() =>
    [
        new ProjectSpec("FilterMini", new (string, string)[]
        {
            ("Core/Widget.cs", WidgetSource),
            ("Utils/Formatter.cs", FormatterSource),
        }),
        new ProjectSpec("FilterMini.Tests", new (string, string)[]
        {
            ("Core/WidgetTests.cs", WidgetTestsSource),
        }, ProjectReferences: ["FilterMini"]),
    ];

    private const string WidgetSource = """
        namespace FilterMini.Core;

        public sealed class Widget
        {
            public string Name { get; }

            public Widget(string name) => Name = name;

            public string Describe() => $"Widget: {Name}";

            private string BuildInternalLabel() => $"[{Name}]";
        }

        """;

    private const string FormatterSource = """
        namespace FilterMini.Utils;

        internal sealed class Formatter
        {
            public string Format(string value) => NormalizeWhitespace(value);

            private static string NormalizeWhitespace(string value) => value.Trim();
        }

        """;

    private const string WidgetTestsSource = """
        using FilterMini.Core;

        namespace FilterMini.Tests.Core;

        public sealed class WidgetTests
        {
            public string DescribeSampleWidget()
            {
                var widget = new Widget(BuildSampleName());
                return widget.Describe();
            }

            private static string BuildSampleName() => "Sample";
        }

        """;
}
