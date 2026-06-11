using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

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

        if (args.Format == "sarif")
        {
            PrintSarifViolations(violations);
            return violations.Count > 0 ? 1 : 0;
        }

        if (violations.Count > 0)
        {
            PrintViolations(violations);
            return 1;
        }

        Console.WriteLine("[SUCCESS]: Alle Prüfungen erfolgreich durchgeführt. Keine Regelverstöße gefunden.");
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

    private static void PrintViolations(IReadOnlyCollection<RuleViolation> violations)
    {
        Console.WriteLine($"\n[INFO]: Es wurden {violations.Count} Regelverstöße gefunden:\n");
        foreach (var violation in violations)
        {
            Console.WriteLine(violation.ToString());
        }
    }

    private static void PrintSarifViolations(IReadOnlyCollection<RuleViolation> violations)
    {
        var doc = new SarifDocument();
        var run = new SarifRun();
        doc.Runs.Add(run);

        foreach (var violation in violations)
        {
            var result = new SarifResult
            {
                RuleId = violation.RuleName ?? "UnknownRule",
            };
            result.Message.Text = $"{violation.Details} Guidance: {violation.Guidance}";
            
            var loc = new SarifLocation();
            loc.PhysicalLocation.ArtifactLocation.Uri = GetFileUri(violation.FilePath);
            loc.PhysicalLocation.Region.StartLine = violation.LineNumber;
            result.Locations.Add(loc);

            run.Results.Add(result);
        }

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(doc, options);
        Console.WriteLine(json);
    }

    private static string GetFileUri(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return "";
        try
        {
            return new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Format error: {ex.Message}");
            return filePath;
        }
    }

    private sealed class SarifDocument
    {
        [JsonPropertyName("$schema")]
        public string Schema => "https://schemastore.azurewebsites.net/schemas/json/sarif-2.1.0-rtm.5.json";
        public string Version => "2.1.0";
        public List<SarifRun> Runs { get; } = new();
    }

    private sealed class SarifRun
    {
        public SarifTool Tool { get; } = new();
        public List<SarifResult> Results { get; } = new();
    }

    private sealed class SarifTool
    {
        public SarifDriver Driver { get; } = new();
    }

    private sealed class SarifDriver
    {
        public string Name => "AiNetLinter";
        public string Version => "1.0.0";
    }

    private sealed class SarifResult
    {
        public string RuleId { get; set; } = "";
        public SarifMessage Message { get; } = new();
        public List<SarifLocation> Locations { get; } = new();
    }

    private sealed class SarifMessage
    {
        public string Text { get; set; } = "";
    }

    private sealed class SarifLocation
    {
        public SarifPhysicalLocation PhysicalLocation { get; } = new();
    }

    private sealed class SarifPhysicalLocation
    {
        public SarifArtifactLocation ArtifactLocation { get; } = new();
        public SarifRegion Region { get; } = new();
    }

    private sealed class SarifArtifactLocation
    {
        public string Uri { get; set; } = "";
    }

    private sealed class SarifRegion
    {
        public int StartLine { get; set; }
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
