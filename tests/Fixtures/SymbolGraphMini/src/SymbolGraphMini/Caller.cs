namespace SymbolGraphMini;

public class Caller
{
    public string Run()
    {
        var greeter = new Greeter();
        return greeter.Greet("World");
    }

    public string RunTwice()
    {
        var greeter = new Greeter();
        return greeter.Greet("World") + " / " + greeter.Greet("World");
    }

    public string RunThrice()
    {
        var greeter = new Greeter();
        return greeter.Greet("World") + " / " + greeter.Greet("World") + " / " + greeter.Greet("World");
    }
}
