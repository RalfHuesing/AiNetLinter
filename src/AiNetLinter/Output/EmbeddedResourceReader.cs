#nullable enable

using System;
using System.IO;
using System.Text;

namespace AiNetLinter.Output;

internal static class EmbeddedResourceReader
{
    internal static string? TryRead(string resourceName)
    {
        using var stream = typeof(EmbeddedResourceReader).Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    internal static string ReadRequired(string resourceName) =>
        TryRead(resourceName) ?? throw new InvalidOperationException(
            $"Die eingebettete Ressource '{resourceName}' wurde nicht gefunden.");
}
