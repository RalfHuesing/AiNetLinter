namespace SymbolGraphMini;

public interface IGreeting
{
    string Greet(string name);
}

public class BaseGreeting : IGreeting
{
    public virtual string Greet(string name) => $"Hi, {name}";
}

public class SpecialGreeting : BaseGreeting
{
}
