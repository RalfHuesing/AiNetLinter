#nullable enable

using System.IO;
using System.Text;
using AiNetLinter.Output;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt die eingebettete README und Konfigurationsdokumentation auf der Konsole aus.
/// </summary>
internal static class ReadmeCommand
{
    /// <summary>
    /// Gibt die eingebetteten Markdown-Dateien (README.md, configuration.md) aus.
    /// </summary>
    internal static int Run(ILintConsole? console = null)
    {
        var c = console ?? ConsoleLintConsole.Instance;
        string[] parts = ["README.md", "Docs/configuration.md"];
        foreach (var name in parts)
        {
            using var stream = typeof(ReadmeCommand).Assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                c.WriteError($"[ERROR]: '{name}' wurde nicht als eingebettete Ressource gefunden.");
                return 1;
            }
            using var reader = new StreamReader(stream, Encoding.UTF8);
            c.WriteLine(reader.ReadToEnd());
        }
        return 0;
    }
}
