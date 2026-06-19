#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Prüft, ob das gespeicherte Playbook mit dem aktuell generierten übereinstimmt.
/// </summary>
internal static class PlaybookCheckCommand
{
    /// <summary>
    /// Führt die Playbook-Drift-Prüfung aus.
    /// </summary>
    internal static async Task<int> RunAsync(LinterArgs args)
    {
        var config = LinterConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath);
        var generatedContent = await RepoPlaybookGenerator.BuildContentAsync(
            catalog.Solution,
            new PlaybookOptions(args.Verbose, config, args.ConfigPath ?? "rules.json"));

        if (!File.Exists(args.PlaybookPath))
        {
            Console.Error.WriteLine($"[ERROR]: Die Playbook-Datei '{args.PlaybookPath}' existiert nicht.");
            return 1;
        }

        var existingContent = await File.ReadAllTextAsync(args.PlaybookPath!, Encoding.UTF8);
        if (generatedContent == existingContent)
        {
            Console.WriteLine("[OK]: Playbook ist aktuell.");
            return 0;
        }

        Console.Error.WriteLine("[ERROR]: Drift erkannt! Das generierte Playbook stimmt nicht mit der Datei auf der Festplatte überein.");
        return 1;
    }
}
