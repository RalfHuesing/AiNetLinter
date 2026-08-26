#nullable enable

namespace AiNetLinter.Mcp.Daemon;

internal static class DaemonInstanceId
{
    internal const int MaxLength = 32;

    internal static string? Validate(string? value)
    {
        if (value is null) return null;
        if (value.Length == 0) return "darf nicht leer sein";
        if (value.Length > MaxLength) return $"darf höchstens {MaxLength} ASCII-Zeichen lang sein";
        if (!IsAsciiLetter(value[0])) return "muss mit einem ASCII-Buchstaben beginnen";

        for (var index = 1; index < value.Length; index++)
        {
            var character = value[index];
            if (!IsAsciiLetter(character) && !IsAsciiDigit(character) && character is not ('.' or '_' or '-'))
            {
                return "darf nur ASCII-Buchstaben, Ziffern sowie '.', '_' oder '-' enthalten";
            }
        }

        return null;
    }

    internal static string? Normalize(string? value)
    {
        var error = Validate(value);
        if (error is not null)
        {
            throw new ArgumentException($"Die Daemon-Instanz-ID {error}.", nameof(value));
        }

        return value?.ToLowerInvariant();
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static bool IsAsciiDigit(char value) => value is >= '0' and <= '9';
}
