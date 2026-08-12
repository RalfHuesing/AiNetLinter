namespace FilterMini.Utils;

internal sealed class Formatter
{
    public string Format(string value) => NormalizeWhitespace(value);

    private static string NormalizeWhitespace(string value) => value.Trim();
}
