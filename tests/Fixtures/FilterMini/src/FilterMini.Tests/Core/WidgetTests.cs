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
