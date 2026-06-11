using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter;

/// <summary>
/// Der CLI-Einstiegspunkt für den Linter.
/// </summary>
public static class Program
{
    /// <summary>
    /// Der Einstiegspunkt für die Ausführung der Linter-CLI.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        var configOpt = new Option<string>(new[] { "--config", "-c" }, "Pfad zur JSON-Konfigurationsdatei (rules.json)") { IsRequired = true };
        var pathOpt = new Option<string>(new[] { "--path", "-p" }, "Pfad zur Solution-Datei (.sln / .slnx) oder ein Verzeichnis") { IsRequired = true };
        var graphOpt = new Option<string?>(new[] { "--graph", "-g" }, "Pfad für das zu generierende Mermaid-Abhängigkeitsdiagramm (.md)");
        var formatOpt = new Option<string>(new[] { "--format", "-f" }, () => "text", "Ausgabeformat: text (Standard) oder sarif");
        var verboseOpt = new Option<bool>(new[] { "--verbose", "-v" }, "Detaillierte Protokollausgabe aktivieren");

        var root = new RootCommand("AiNetLinter - CLI-Linter für AI-optimierten .NET Code")
        {
            configOpt, pathOpt, graphOpt, formatOpt, verboseOpt
        };

        root.SetHandler(async context =>
        {
            try
            {
                var linterArgs = new LinterArgs
                {
                    ConfigPath = context.ParseResult.GetValueForOption(configOpt) ?? "",
                    TargetPath = context.ParseResult.GetValueForOption(pathOpt) ?? "",
                    GraphPath = context.ParseResult.GetValueForOption(graphOpt),
                    Format = context.ParseResult.GetValueForOption(formatOpt) ?? "text",
                    Verbose = context.ParseResult.GetValueForOption(verboseOpt)
                };

                var exitCode = await ExecuteLinterAsync(linterArgs);
                context.ExitCode = exitCode;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                context.ExitCode = 2;
            }
        });

        return await root.InvokeAsync(args);
    }

    private static void LogStart(bool verbose, string configPath, string targetPath)
    {
        if (verbose)
        {
            Console.WriteLine($"[INFO]: Lade Konfiguration von: {configPath}");
            Console.WriteLine($"[INFO]: Analysiere Ziel-Pfad: {targetPath}");
        }
    }

    private static LinterConfig? TryLoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Konfigurationsdatei wurde nicht gefunden: {configPath}");
            return null;
        }

        var config = LoadConfig(configPath);
        if (config == null)
        {
            Console.Error.WriteLine("[ERROR]: Die Konfigurationsdatei konnte nicht deserialisiert werden.");
        }
        return config;
    }

    private static async Task<int> ExecuteLinterAsync(LinterArgs args)
    {
        LogStart(args.Verbose, args.ConfigPath, args.TargetPath);

        var config = TryLoadConfig(args.ConfigPath);
        if (config == null)
        {
            return 1;
        }

        var engine = new LinterEngine(config);
        var violations = await engine.RunAsync(args.TargetPath);
        var outputRoot = OutputRootResolver.Resolve(args.TargetPath);

        if (args.Format == "sarif")
        {
            SarifWriter.Write(violations, outputRoot);
            return violations.Count > 0 ? 1 : 0;
        }

        if (violations.Count > 0)
        {
            Console.WriteLine(ViolationTextFormatter.Format(violations, outputRoot));
            return 1;
        }

        Console.WriteLine("OK");
        return 0;
    }

    private static LinterConfig? LoadConfig(string configPath)
    {
        try
        {
            var content = File.ReadAllText(configPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<LinterConfig>(content, options);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            return null;
        }
    }

    private sealed class LinterArgs
    {
        public required string ConfigPath { get; init; }
        public required string TargetPath { get; init; }
        public string? GraphPath { get; init; }
        public required string Format { get; init; }
        public required bool Verbose { get; init; }
    }
}
