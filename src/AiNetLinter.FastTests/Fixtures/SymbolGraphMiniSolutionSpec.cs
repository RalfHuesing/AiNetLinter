#nullable enable

using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Fixtures;

internal static class SymbolGraphMiniSolutionSpec
{
    public const string GreeterPath = @"C:\ainetlinter-virtual\src\SymbolGraphMini\Greeter.cs";
    public const string CallerPath = @"C:\ainetlinter-virtual\src\SymbolGraphMini\Caller.cs";
    public const string OtherCallerPath = @"C:\ainetlinter-virtual\src\SymbolGraphMini\OtherCaller.cs";

    public static RoslynTestSolution Create() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\ainetlinter-virtual\SymbolGraphMini.slnx",
        new ProjectSpec("SymbolGraphMini", Documents, VirtualProjectDirectory: "src/SymbolGraphMini"));

    private static readonly (string FileName, string Content)[] Documents = [
        ("Greeter.cs", """
            namespace SymbolGraphMini;

            public class Greeter
            {
                public string Greet(string name) => $"Hello, {name}";

                public string Prefix { get; set; } = "Hi";
            }
            """),
        ("Records.cs", """
            namespace SymbolGraphMini;

            public class GreetingClass
            {
            }

            public record GreetingRecord(string Text);
            """),
        ("Caller.cs", """
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
            """),
        ("OtherCaller.cs", """
            namespace SymbolGraphMini;

            public class OtherCaller
            {
                public string Run() => "other";
            }
            """),
        ("Hierarchy.cs", """
            using System;

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

            public sealed class DisposableGreeting : IDisposable
            {
                public void Dispose()
                {
                }
            }
            """),
        ("ViolationTrigger.cs", """
            #nullable enable

            namespace SymbolGraphMini;

            public class ViolationTrigger
            {
                public void DoWork()
                {
                }
            }
            """)
    ];
}
