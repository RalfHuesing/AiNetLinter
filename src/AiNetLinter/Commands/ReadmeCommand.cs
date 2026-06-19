#nullable enable

using System;
using System.IO;
using System.Text;

namespace AiNetLinter.Commands;

/// <summary>
/// Gibt die eingebettete README und Konfigurationsdokumentation auf der Konsole aus.
/// </summary>
internal static class ReadmeCommand
{
    /// <summary>
    /// Gibt die eingebetteten Markdown-Dateien (README.md, configuration.md) aus.
    /// </summary>
    internal static int Run()
    {
        string[] parts = ["README.md", "Docs/configuration.md"];
        foreach (var name in parts)
        {
            using var stream = typeof(ReadmeCommand).Assembly.GetManifestResourceStream(name);
            if (stream == null)
            {
                Console.Error.WriteLine($"[ERROR]: '{name}' wurde nicht als eingebettete Ressource gefunden.");
                return 1;
            }
            using var reader = new StreamReader(stream, Encoding.UTF8);
            Console.WriteLine(reader.ReadToEnd());
        }
        return 0;
    }
}
