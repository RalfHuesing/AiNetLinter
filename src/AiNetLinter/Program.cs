using System.Text.Json;
using AiNetLinter.Configuration;
using AiNetLinter.Core;

namespace AiNetLinter;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            // Simple CLI-Argumenten-Parser
            string? configPath = null;
            string? targetPath = null;
            bool verbose = false;

            for (int i = 0; i < args.Length; i++)
            {
                if ((args[i] == "--config" || args[i] == "-c") && i + 1 < args.Length)
                {
                    configPath = args[++i];
                }
                else if ((args[i] == "--path" || args[i] == "-p") && i + 1 < args.Length)
                {
                    targetPath = args[++i];
                }
                else if (args[i] == "--verbose" || args[i] == "-v")
                {
                    verbose = true;
                }
            }

            if (string.IsNullOrWhiteSpace(configPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                ShowUsage();
                return 1;
            }

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

            // Konfiguration deserialisieren
            var configContent = File.ReadAllText(configPath);
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LinterConfig>(configContent, jsonOptions);

            if (config == null)
            {
                Console.Error.WriteLine("[ERROR]: Die Konfigurationsdatei konnte nicht deserialisiert werden.");
                return 1;
            }

            // Engine starten
            var engine = new LinterEngine(config);
            var violations = engine.Run(targetPath);

            if (violations.Count > 0)
            {
                Console.WriteLine($"\n[INFO]: Es wurden {violations.Count} Regelverstöße gefunden:\n");
                foreach (var violation in violations)
                {
                    Console.WriteLine(violation.ToString());
                }
                return 1;
            }

            Console.WriteLine("[SUCCESS]: Alle Prüfungen erfolgreich durchgeführt. Keine Regelverstöße gefunden.");
            return 0;
        }
        catch (Exception ex)
        {
            // Exceptions nicht stumm schlucken
            Console.Error.WriteLine($"[FATAL ERROR]: Ein unerwarteter Fehler ist aufgetreten: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
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
