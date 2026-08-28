#nullable enable

namespace AiNetLinter.Configuration;

internal static class ExternalSourcePathRules
{
    internal static bool IsDriveQualified(string value) =>
        value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':';
}
