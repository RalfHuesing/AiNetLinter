namespace SymbolGraphMini;

public class Caller
{
    public string Run()
    {
        var greeter = new Greeter();
        return greeter.Greet("World");
    }
}
