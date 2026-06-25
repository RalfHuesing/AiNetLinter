#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.Cli;
using AiNetLinter.Evals;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Assembliert einen vollständigen Eval-Audit-Prompt und gibt ihn auf stdout aus.
/// </summary>
internal static class EvalCommand
{
    internal static Task<int> RunAsync(
        LinterArgs args,
        CancellationToken ct = default,
        ILintConsole? console = null)
    {
        var c = console ?? LinterConsole.Instance;

        if (string.IsNullOrWhiteSpace(args.TargetPath))
        {
            c.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ConfigRequired,
                "--path fehlt für --eval",
                context: $"--eval {args.EvalType}",
                hint: "Pfad zur Solution oder zum Verzeichnis mit --path angeben."));
            return Task.FromResult(1);
        }

        var eval = EvalRegistry.TryResolve(args.EvalType ?? "");
        if (eval == null)
        {
            c.WriteError(LinterErrorFormatter.Format(
                LinterErrorCodes.ResourceNotFound,
                $"Unbekannter Eval-Typ '{args.EvalType}'",
                context: $"--eval {args.EvalType}",
                hint: $"Verfügbare Typen: {string.Join(", ", EvalRegistry.All.Select(e => e.Name))}\n  Vollständige Liste mit --list-evals abrufen."));
            return Task.FromResult(1);
        }

        var spec    = SpecLoader.Load(args.SpecPaths ?? []);
        var prompt  = EvalAssembler.Assemble(eval, args.TargetPath, spec,
                          DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

        c.WriteLine(prompt);
        return Task.FromResult(0);
    }
}
