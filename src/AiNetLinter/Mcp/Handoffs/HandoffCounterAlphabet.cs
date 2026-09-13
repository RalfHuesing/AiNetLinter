#nullable enable

using System;

namespace AiNetLinter.Mcp.Handoffs;

/// <summary>
/// Reines Berechnungs- und Validierungsmodul für das alphabetische Handoff-Counter-Format (h:...).
/// </summary>
internal static class HandoffCounterAlphabet
{
    internal const string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    internal const string HandlePrefix = "h:";
    internal const string FirstCounter = "a";

    internal static bool IsValidCounterChar(char character) =>
        (character >= 'a' && character <= 'z')
        || (character >= '0' && character <= '9')
        || (character >= 'A' && character <= 'Z');

    internal static bool IsValidCounter(string? counter)
    {
        if (string.IsNullOrEmpty(counter)) return false;
        foreach (var character in counter)
        {
            if (!IsValidCounterChar(character)) return false;
        }
        return true;
    }

    internal static bool IsValidHandle(string? handle)
    {
        if (string.IsNullOrEmpty(handle) || handle.Length <= HandlePrefix.Length) return false;
        if (!handle.StartsWith(HandlePrefix, StringComparison.Ordinal)) return false;

        return IsValidCounter(handle[HandlePrefix.Length..]);
    }

    internal static bool IsWindowsDrivePath(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length < 3) return false;
        return char.IsLetter(value[0])
            && value[1] == ':'
            && (value[2] == '\\' || value[2] == '/');
    }

    internal static string FormatHandle(string counter) => HandlePrefix + counter;

    internal static bool TryExtractCounter(string? handle, out string counter)
    {
        counter = string.Empty;
        if (!IsValidHandle(handle)) return false;

        counter = handle![HandlePrefix.Length..];
        return true;
    }

    internal static string GetNext(string current)
    {
        if (string.IsNullOrEmpty(current))
        {
            throw new ArgumentException("Der aktuelle Zähler darf nicht leer sein.", nameof(current));
        }

        if (!IsValidCounter(current))
        {
            throw new ArgumentException($"Der Zähler '{current}' enthält ungültige Zeichen.", nameof(current));
        }

        var characters = current.ToCharArray();
        var carry = true;

        for (var i = characters.Length - 1; i >= 0 && carry; i--)
        {
            var charIndex = Alphabet.IndexOf(characters[i]);
            if (charIndex < 0)
            {
                throw new ArgumentException($"Das Zeichen '{characters[i]}' gehört nicht zum Handoff-Alphabet.", nameof(current));
            }

            if (charIndex == Alphabet.Length - 1)
            {
                characters[i] = Alphabet[0];
                carry = true;
            }
            else
            {
                characters[i] = Alphabet[charIndex + 1];
                carry = false;
            }
        }

        if (carry)
        {
            return Alphabet[0] + new string(characters);
        }

        return new string(characters);
    }
}
