#nullable enable

using System.Text;
using AiNetLinter.Evals;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt alle verfügbaren Eval-Typen als Tabelle aus.
/// </summary>
internal static class ListEvalsCommand
{
    internal static int Run(ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;
        var sb = new StringBuilder();

        sb.AppendLine("# AiNetLinter — Eval-Übersicht");
        sb.AppendLine();
        sb.AppendLine("Assembliert vollständige Audit-Prompts inkl. Evidenz.");
        sb.AppendLine("Nutzung: `ainetlinter --eval <name> --path <pfad> [--spec <pfad> ...]`");
        sb.AppendLine();
        sb.AppendLine("| Name | Bezeichnung | Evidenz | --path |");
        sb.AppendLine("|:---|:---|:---|:---|");

        foreach (var eval in EvalRegistry.All)
        {
            var evidence = eval.Evidence switch
            {
                EvalEvidenceType.Vocabulary => "vocabulary map",
                EvalEvidenceType.Structure  => "structure map",
                _ => "-"
            };
            sb.AppendLine($"| {eval.Name} | {eval.DisplayName} | {evidence} | ja |");
        }

        sb.AppendLine();
        sb.AppendLine("Beschreibungen:");
        foreach (var eval in EvalRegistry.All)
            sb.AppendLine($"- **{eval.Name}:** {eval.Description}");

        c.WriteLine(sb.ToString().TrimEnd());
        return 0;
    }
}
