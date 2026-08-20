#nullable enable

using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Fixtures;

internal static class TransitiveSymbolGraphMiniSolutionSpec
{
    public static RoslynTestSolution Create() => RoslynTestSolutionFactory.CreateSolution(
        @"C:\ainetlinter-virtual\TransitiveSymbolGraphMini.slnx",
        new ProjectSpec("Contracts", [
            ("Processor.cs", """
                namespace Contracts;

                public interface IProcessor
                {
                    string Execute();
                }

                public class BaseProcessor : IProcessor
                {
                    public virtual string Execute() => "base";
                }

                public class DerivedProcessor : BaseProcessor
                {
                    public override string Execute() => base.Execute();
                }

                public class MoreDerivedProcessor : DerivedProcessor
                {
                    public override string Execute() => base.Execute();
                }
                """)
        ]),
        new ProjectSpec("Application", [
            ("ProcessorCaller.cs", """
                using Contracts;

                namespace Application;

                public sealed class ProcessorCaller
                {
                    public string Run(IProcessor processor) => processor.Execute();
                }
                """)
        ], ["Contracts"]));
}
