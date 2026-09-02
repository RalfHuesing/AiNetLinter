#nullable enable

using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Fixtures;

internal static class CompileErrorMiniSolutionSpec
{
    public static RoslynTestSolution CreatePlural() => RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\CompileErrorMini.slnx", new ProjectSpec("CompileErrorMini", [
        ("ValidClassA.cs", "namespace CompileErrorMini; public class ValidClassA { public void DoWork() { } public int Compute(int x) => x; }"),
        ("ValidClassB.cs", "namespace CompileErrorMini; public class ValidClassB { public string Greet(string name) => name; }"),
        ("ValidClassC.cs", "namespace CompileErrorMini.Sub; public class ValidClassC { public void Process() { } }"),
        ("BrokenClassA.cs", "public class BrokenClassA { public void F( { } }"),
        ("BrokenClassB.cs", "public class BrokenClassB : DoesNotExist { }"),
        ("BrokenClassC.cs", "public class BrokenClassC { UndefinedType Value; }")
    ], VirtualProjectDirectory: "src/CompileErrorMini"));

}
