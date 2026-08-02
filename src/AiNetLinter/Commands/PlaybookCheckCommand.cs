#nullable enable

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Baseline;
using AiNetLinter.Cli;
using AiNetLinter.Configuration;
using AiNetLinter.Core;
using AiNetLinter.Generators;
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
    internal static async Task<int> RunAsync(LinterArgs args, CancellationToken ct = default, ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;
        var config = ConfigLoader.TryLoadConfig(args.ConfigPath, isRequired: false);

        using var catalog = await SourceFileCatalog.LoadAsync(args.TargetPath, ct);
        var generatedContent = await RepoPlaybookGenerator.BuildContentAsync(
            catalog.Solution,
            new PlaybookOptions(args.Verbose, config, args.ConfigPath ?? "rules.json"));

        if (!File.Exists(args.PlaybookPath))
        {
            c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.ResourceNotFound,
                "Die Playbook-Datei existiert nicht.",
                context: args.PlaybookPath,
                hint: "Playbook mit --playbook <pfad> erzeugen (ohne --check)."));
            return 1;
        }

        var existingContent = await File.ReadAllTextAsync(args.PlaybookPath!, Encoding.UTF8);
        if (generatedContent == existingContent)
        {
            c.WriteLine("[OK]: Playbook ist aktuell.");
            return 0;
        }

        c.WriteError(LinterErrorFormatter.Format(LinterErrorCodes.DriftDetected,
            "Das generierte Playbook stimmt nicht mit der gespeicherten Datei ueberein.",
            context: args.PlaybookPath,
            hint: "Playbook mit --playbook <pfad> (ohne --check) neu generieren."));
        return 1;
    }
}
