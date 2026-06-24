#nullable enable

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AiNetLinter.Web;

/// <summary>
/// Helper-Methoden fuer <see cref="RazorAnalyzer"/> (separate Datei wegen MaxLineCount-Limit
/// und zur Reduktion der kognitiven Komplexitaet der Checker-Methoden).
/// </summary>
internal static partial class RazorAnalyzer
{
    /// <summary>
    /// Prueft, ob ein Attribut-Name ein Event-Handler (on*) oder Binding (bind*) ist.
    /// </summary>
    private static bool IsEventOrBindingAttribute(string name) =>
        name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("bind", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Bestimmt, ob ein Attributwert ein komplexes Lambda enthaelt.
    /// Komplex = enthaelt Block-Klammern (mehrzeilig) oder mehr als ein Statement.
    /// Triviale Einzeiler-Lambdas wie '() => Count++' sind erlaubt.
    /// </summary>
    private static bool IsComplexLambda(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (!value.Contains("=>", StringComparison.Ordinal)) return false;
        if (value.Contains('{', StringComparison.Ordinal)) return true;
        if (value.Contains('}', StringComparison.Ordinal)) return true;

        var firstSemi = value.IndexOf(';');
        if (firstSemi < 0) return false;
        // Mehr als ein Semikolon = mehrzeilig.
        return value.IndexOf(';', firstSemi + 1) >= 0;
    }

    /// <summary>
    /// Zaehlt Attribute in einem Attribut-Block (inkl. @-Praefix und ':event'-Modifikatoren).
    /// </summary>
    internal static int CountAttributes(string attrs)
    {
        if (string.IsNullOrWhiteSpace(attrs)) return 0;
        return AttributePattern.Matches(attrs).Count;
    }

    /// <summary>
    /// Ersetzt Razor- und HTML-Kommentare durch Leerzeichen gleicher Laenge
    /// (Zeilennummern bleiben erhalten).
    /// </summary>
    private static string StripComments(string content) =>
        RazorCommentPattern.Replace(content, m => new string(' ', m.Length));

    /// <summary>
    /// Berechnet die maximale Verschachtelungstiefe der oeffnenden HTML-Tags.
    /// Self-closing Tags (<br/>, <MyComp />) und Void-Elemente (<input>)
    /// zaehlen nicht zur Tiefe.
    /// </summary>
    internal static int ComputeMaxTagNestingDepth(string sanitized)
    {
        var events = new List<(int Index, bool IsOpen)>();

        foreach (Match m in OpeningTagPattern.Matches(sanitized))
        {
            var isSelfClosing = m.Groups[3].Value == "/";
            if (isSelfClosing || VoidElements.Contains(m.Groups[1].Value))
            {
                continue;
            }
            events.Add((m.Index, true));
        }

        foreach (Match m in ClosingTagPattern.Matches(sanitized))
        {
            events.Add((m.Index, false));
        }

        events.Sort((a, b) => a.Index.CompareTo(b.Index));

        var openCount = 0;
        var maxDepth = 0;
        foreach (var ev in events)
        {
            if (ev.IsOpen)
            {
                openCount++;
                if (openCount > maxDepth) maxDepth = openCount;
            }
            else
            {
                if (openCount > 0) openCount--;
            }
        }
        return maxDepth;
    }

    /// <summary>
    /// Berechnet die maximale Verschachtelungstiefe von @foreach-Schleifen.
    /// </summary>
    internal static int ComputeMaxForeachNestingDepth(string content)
    {
        var positions = new List<int>();
        foreach (Match m in ForeachPattern.Matches(content))
        {
            var prev = m.Index > 0 ? content[m.Index - 1] : ' ';
            if (char.IsLetterOrDigit(prev) || prev == '_') continue;
            positions.Add(m.Index);
        }
        if (positions.Count == 0) return 0;

        var intervals = new List<(int Start, int End)>();
        foreach (var pos in positions)
        {
            var bodyEnd = FindForeachBodyEnd(content, pos);
            if (bodyEnd > 0) intervals.Add((pos, bodyEnd));
        }
        if (intervals.Count == 0) return 0;

        var maxDepth = 0;
        foreach (var iv in intervals)
        {
            var depth = 1;
            foreach (var other in intervals)
            {
                if (other.Start == iv.Start && other.End == iv.End) continue;
                if (other.Start <= iv.Start && iv.End <= other.End) depth++;
            }
            if (depth > maxDepth) maxDepth = depth;
        }
        return maxDepth;
    }

    /// <summary>
    /// Findet das Ende des @foreach-Body-Blocks (Position der schliessenden '}').
    /// </summary>
    private static int FindForeachBodyEnd(string content, int foreachPos)
    {
        // Suche erste '{' nach '@foreach (...)'.
        var openBrace = content.IndexOf('{', foreachPos + "@foreach".Length);
        if (openBrace < 0) return -1;

        var depth = 1;
        var i = openBrace + 1;
        while (i < content.Length && depth > 0)
        {
            i = SkipStringContext(content, i, out var endedInString);
            if (endedInString) return -1; // Unbalancierte Anfuehrungszeichen → kein Match.

            var c = content[i];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            i++;
        }

        return depth == 0 ? i - 1 : -1;
    }

    /// <summary>
    /// Findet die Position der zu 'openBracePos' gehoerenden schliessenden Klammer.
    /// </summary>
    private static int FindMatchingBrace(string content, int openBracePos)
    {
        if (openBracePos >= content.Length || content[openBracePos] != '{') return -1;

        var depth = 1;
        var i = openBracePos + 1;
        while (i < content.Length && depth > 0)
        {
            i = SkipStringContext(content, i, out var endedInString);
            if (endedInString) return -1;

            var c = content[i];
            if (c == '{') depth++;
            else if (c == '}') depth--;
            i++;
        }
        return depth == 0 ? i - 1 : -1;
    }

    /// <summary>
    /// Ueberspringt String-/Char-Literal- und Razor-Kommentar-Bereiche ab Position 'i'.
    /// Liefert die neue Position und gibt an, ob ein String/Kommentar unbalanciert
    /// endete (dann sollte der Aufrufer abbrechen).
    /// </summary>
    private static int SkipStringContext(string content, int i, out bool endedInString)
    {
        endedInString = false;
        if (i >= content.Length) return i;

        var c = content[i];
        // Razor-Kommentar: @* ... *@
        if (c == '@' && i + 1 < content.Length && content[i + 1] == '*')
        {
            var end = content.IndexOf("*@", i + 2, StringComparison.Ordinal);
            if (end < 0) { endedInString = true; return content.Length; }
            return end + 2;
        }
        // String- oder Char-Literal.
        if (c == '"' || c == '\'')
        {
            var quote = c;
            var j = i + 1;
            while (j < content.Length)
            {
                if (content[j] == '\\' && j + 1 < content.Length) { j += 2; continue; }
                if (content[j] == quote) return j + 1;
                j++;
            }
            endedInString = true;
            return content.Length;
        }
        return i;
    }

    private static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content)) return 0;
        var n = 1;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n') n++;
        }
        return n;
    }

    private static int GetLineNumber(string content, int position)
    {
        if (position <= 0) return 1;
        var line = 1;
        var max = Math.Min(position, content.Length);
        for (int i = 0; i < max; i++)
        {
            if (content[i] == '\n') line++;
        }
        return line;
    }

    private static (int StartLine, int EndLine) GetLineRange(
        string content, int startPos, int endPos) =>
        (GetLineNumber(content, startPos), GetLineNumber(content, endPos));

    /// <summary>
    /// Extrahiert den Komponentennamen aus einem .razor-Dateipfad
    /// (z. B. 'C:\app\Pages\Counter.razor' → 'Counter').
    /// </summary>
    private static string ExtractComponentNameFromPath(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return string.Empty;
        var sep = Math.Max(filePath.LastIndexOf('/'), filePath.LastIndexOf('\\'));
        var fileName = sep >= 0 ? filePath[(sep + 1)..] : filePath;
        const string suffix = ".razor";
        return fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^suffix.Length]
            : fileName;
    }
}