namespace FilterMini.Core;

public sealed class Widget
{
    public string Name { get; }

    public Widget(string name) => Name = name;

    public string Describe() => $"Widget: {Name}";

    private string BuildInternalLabel() => $"[{Name}]";
}
