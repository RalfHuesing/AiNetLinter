#nullable enable

using AiNetLinter.TestKit;

namespace AiNetLinter.FastTests.Fixtures;

internal static class DiRegistrationMiniSolutionSpec
{
    public static RoslynTestSolution Create() => RoslynTestSolutionFactory.CreateSolution(@"C:\ainetlinter-virtual\DiRegistrationMini.slnx", new ProjectSpec("DiRegistrationMini", [
        ("Program.cs", """
            namespace Microsoft.Extensions.DependencyInjection;
            public interface IServiceCollection { }
            public static class ServiceCollectionExtensions
            {
                public static void AddScoped<TService, TImplementation>(this IServiceCollection services) { }
                public static void AddSingleton<TService>(this IServiceCollection services) { }
                public static void AddTransient<TService>(this IServiceCollection services) { }
            }
            namespace DiRegistrationMini;
            using Microsoft.Extensions.DependencyInjection;
            public interface IReporter { void Report(string value); }
            public class ConsoleReporter : IReporter { public void Report(string value) { } }
            public static class Composition
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddScoped<IReporter, ConsoleReporter>();
                    services.AddSingleton<IReporter>();
                    services.AddTransient<IReporter>();
                    var MyAddScopedHelper = "not a match";
                }
            }
            """)
    ], VirtualProjectDirectory: "src/DiRegistrationMini"));
}
