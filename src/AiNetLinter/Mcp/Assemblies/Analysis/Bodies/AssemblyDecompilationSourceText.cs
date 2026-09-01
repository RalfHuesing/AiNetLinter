#nullable enable

using System;
using System.Linq;

namespace AiNetLinter.Mcp.Assemblies.Analysis.Bodies;

internal static class AssemblyDecompilationSourceText
{
    internal static string RemoveCompilerGeneratedNestedTypes(string source)
    {
        while (true)
        {
            var typeStart = FindCompilerGeneratedTypeStart(source);
            if (typeStart < 0) return source;

            var openingBrace = source.IndexOf('{', typeStart);
            var closingBrace = openingBrace < 0 ? -1 : FindMatchingBrace(source, openingBrace);
            if (closingBrace < 0) return source;
            source = source.Remove(typeStart, closingBrace - typeStart);
        }
    }

    internal static string RemoveCompilerGeneratedStateMachineAttributes(string source) =>
        string.Join(
            Environment.NewLine,
            source.Split(Environment.NewLine)
                .Where(line => !line.Contains("[AsyncStateMachine(", StringComparison.Ordinal)
                    && !line.Contains("[IteratorStateMachine(", StringComparison.Ordinal)));

    private static int FindCompilerGeneratedTypeStart(string source)
    {
        var markers = new[] { "class <", "struct <", "interface <", "record <", "delegate <", "enum <" };
        var markerIndex = markers
            .Select(marker => source.IndexOf(marker, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (markerIndex < 0) return -1;

        var lineStart = source.LastIndexOf('\n', markerIndex) + 1;
        var attributeStart = lineStart;
        while (attributeStart > 0)
        {
            var previousLineEnd = attributeStart - 1;
            var previousLineStart = source.LastIndexOf('\n', Math.Max(0, previousLineEnd - 1)) + 1;
            var previousLine = source[previousLineStart..previousLineEnd].Trim();
            if (!previousLine.StartsWith("[", StringComparison.Ordinal)
                || !previousLine.Contains("CompilerGenerated", StringComparison.Ordinal)) break;
            attributeStart = previousLineStart;
        }

        return attributeStart;
    }

    private static int FindMatchingBrace(string source, int openingBrace)
    {
        var depth = 0;
        var state = new BraceScannerState();
        for (var index = openingBrace; index < source.Length; index++)
        {
            if (SkipIgnoredCharacter(source, ref index, ref state)) continue;

            var character = source[index];
            if (character == '{') depth++;
            else if (character == '}' && --depth == 0) return index + 1;
        }

        return -1;
    }

    private static bool SkipIgnoredCharacter(string source, ref int index, ref BraceScannerState state) =>
        SkipLineComment(source, ref index, ref state)
        || SkipBlockComment(source, ref index, ref state)
        || SkipString(source, ref index, ref state)
        || SkipCharacter(source, ref index, ref state)
        || EnterIgnoredRegion(source, ref index, ref state);

    private static bool SkipLineComment(string source, ref int index, ref BraceScannerState state)
    {
        if (!state.InLineComment) return false;
        if (source[index] is '\r' or '\n') state.InLineComment = false;
        return true;
    }

    private static bool SkipBlockComment(string source, ref int index, ref BraceScannerState state)
    {
        if (!state.InBlockComment) return false;
        if (source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
        {
            state.InBlockComment = false;
            index++;
        }

        return true;
    }

    private static bool SkipString(string source, ref int index, ref BraceScannerState state)
    {
        if (!state.InString) return false;
        if (state.IsVerbatimString)
        {
            if (source[index] == '"')
            {
                if (index + 1 < source.Length && source[index + 1] == '"') index++;
                else state.InString = false;
            }
        }
        else if (source[index] == '\\') index++;
        else if (source[index] == '"') state.InString = false;

        return true;
    }

    private static bool SkipCharacter(string source, ref int index, ref BraceScannerState state)
    {
        if (!state.InCharacter) return false;
        if (source[index] == '\\') index++;
        else if (source[index] == '\'') state.InCharacter = false;
        return true;
    }

    private static bool EnterIgnoredRegion(string source, ref int index, ref BraceScannerState state)
    {
        if (source[index] == '/' && index + 1 < source.Length)
        {
            if (source[index + 1] == '/')
            {
                state.InLineComment = true;
                index++;
                return true;
            }

            if (source[index + 1] == '*')
            {
                state.InBlockComment = true;
                index++;
                return true;
            }
        }

        if (source[index] == '"')
        {
            state.InString = true;
            state.IsVerbatimString = index > 0 && source[index - 1] == '@';
            return true;
        }

        if (source[index] == '\'')
        {
            state.InCharacter = true;
            return true;
        }

        return false;
    }

    private struct BraceScannerState
    {
        internal bool InString;
        internal bool IsVerbatimString;
        internal bool InCharacter;
        internal bool InLineComment;
        internal bool InBlockComment;
    }
}
