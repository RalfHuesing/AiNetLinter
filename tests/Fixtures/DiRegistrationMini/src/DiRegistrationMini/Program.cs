namespace DiRegistrationMini;

public interface IReporter { void Report(string s); }
public class ConsoleReporter : IReporter { public void Report(string s) {} }
public static class Composition
{
    public static void Register(object services)
    {
        ((dynamic)services).AddScoped<IReporter, ConsoleReporter>();
        ((dynamic)services).AddSingleton<IReporter>();
        ((dynamic)services).AddTransient<IReporter>();
        var MyAddScopedHelper = "not a match";
    }
}
