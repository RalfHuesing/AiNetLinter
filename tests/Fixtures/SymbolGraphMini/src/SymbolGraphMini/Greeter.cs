namespace SymbolGraphMini;

public class Greeter
{
    public string Greet(string name) => $"Hello, {name}";

    public string Prefix { get; set; } = "Hi";
}
