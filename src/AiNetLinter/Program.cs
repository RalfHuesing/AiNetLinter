using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Models;

namespace AiNetLinter;

/// <summary>
/// Der CLI-Einstiegspunkt für den Linter.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var (configPath, targetPath, verbose) = ParseArguments(args);
            if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                ShowUsage();
                return 1;
            }

            return ExecuteLinter(configPath, targetPath, verbose);
        }
        catch (Exception ex)
        {
            // Exceptions nicht stumm schlucken
            Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    private static (string? configPath, string? targetPath, bool verbose) ParseArguments(string[] args)
    {
        string? configPath = FindArgument(args, "--config", "-c");
        string? targetPath = FindArgument(args, "--path", "-p");
        bool verbose = args.Contains("--verbose") || args.Contains("-v");
        return (configPath, targetPath, verbose);
    }

    private static string? FindArgument(string[] args, string longName, string shortName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == longName || args[i] == shortName)
            {
                return args[i + 1];
            }
        }
        return null;
    }

    private static int ExecuteLinter(string configPath, string targetPath, bool verbose)
    {
        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Konfigurationsdatei wurde nicht gefunden: {configPath}");
            return 1;
        }

        if (verbose)
        {
            Console.WriteLine($"[INFO]: Lade Konfiguration von: {configPath}");
            Console.WriteLine($"[INFO]: Analysiere Ziel-Pfad: {targetPath}");
        }

        var config = LoadConfig(configPath);
        if (config == null)
        {
            Console.Error.WriteLine("[ERROR]: Die Konfigurationsdatei konnte nicht deserialisiert werden.");
            return 1;
        }

        var engine = new LinterEngine(config);
        var violations = engine.Run(targetPath);

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
        catch
        {
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

    private static void ShowUsage()
    {
        Console.WriteLine("AiNetLinter - CLI-Linter für AI-optimierten .NET Code\n");
        Console.WriteLine("Verwendung:");
        Console.WriteLine("  ainetlinter --config <Pfad-zu-rules.json> --path <Pfad-zur-Solution-oder-Ordner>");
        Console.WriteLine("\nOptionen:");
        Console.WriteLine("  -c, --config    Pfad zur JSON-Konfigurationsdatei (rules.json) (Erforderlich)");
        Console.WriteLine("  -p, --path      Pfad zur .slnx, .csproj, einer .cs Datei oder einem Verzeichnis (Erforderlich)");
        Console.WriteLine("  -v, --verbose   Detaillierte Protokollausgabe aktivieren");
    }
}
