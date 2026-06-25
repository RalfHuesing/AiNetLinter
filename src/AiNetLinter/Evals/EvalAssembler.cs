#nullable enable

using System;
using System.IO;
using System.Text;
using AiNetLinter.Maps;
using AiNetLinter.Output;

namespace AiNetLinter.Evals;

/// <summary>
/// Lädt das eingebettete Template eines Eval-Typs, ersetzt alle Platzhalter
/// und gibt den assemblierten Prompt zurück.
/// </summary>
internal static class EvalAssembler
{
    internal static string Assemble(
        EvalDefinition eval,
        string targetPath,
        string spec,
        string generatedAt)
    {
        var template = LoadTemplate(eval.Name);
        var evidence = BuildEvidence(eval, targetPath);

        return template
            .Replace("{{SPEC}}",           spec)
            .Replace("{{VOCABULARY_MAP}}", eval.Evidence == EvalEvidenceType.Vocabulary ? evidence : "")
            .Replace("{{STRUCTURE_MAP}}",  eval.Evidence == EvalEvidenceType.Structure  ? evidence : "")
            .Replace("{{GENERATED_AT}}",   generatedAt)
            .Replace("{{TARGET_PATH}}",    targetPath.Replace('\\', '/'));
    }

    private static string LoadTemplate(string evalName)
    {
        var resourceName = $"Docs/Evals/{evalName}.md";
        using var stream = typeof(EvalAssembler).Assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
            throw new InvalidOperationException(
                $"Eingebettetes Template '{resourceName}' nicht gefunden. " +
                "Prüfe ob die Datei in AiNetLinter.csproj als EmbeddedResource registriert ist.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string BuildEvidence(EvalDefinition eval, string targetPath)
    {
        var collector = new StringLintConsole();

        return eval.Evidence switch
        {
            EvalEvidenceType.Vocabulary => BuildVocabularyEvidence(targetPath, collector),
            EvalEvidenceType.Structure  => BuildStructureEvidence(targetPath, collector),
            _ => ""
        };
    }

    private static string BuildVocabularyEvidence(string targetPath, StringLintConsole collector)
    {
        VocabularyMapBuilder.Build(targetPath, collector);
        return collector.Output;
    }

    private static string BuildStructureEvidence(string targetPath, StringLintConsole collector)
    {
        // MaxLineCount: Default 500 (kein Config-Load nötig, da --eval kein --config erfordert)
        StructureMapBuilder.Build(targetPath, maxLineCount: 500, collector);
        return collector.Output;
    }

    /// <summary>
    /// Interne Konsole die alles in einen String sammelt statt auf stdout auszugeben.
    /// </summary>
    private sealed class StringLintConsole : ILintConsole
    {
        private readonly StringBuilder _sb = new();
        public string Output => _sb.ToString();
        public void WriteLine(string message) => _sb.AppendLine(message);
        public void WriteError(string message) { /* Fehler beim Evidence-Build ignorieren */ }
    }
}
