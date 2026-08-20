#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace AiNetLinter.Output;

internal enum ColumnAlign
{
    Left,
    Right,
    Center,
}

internal sealed class MarkdownTableBuilder
{
    private readonly List<(string Header, ColumnAlign Align)> _columns = new();
    private readonly List<string[]> _rows = new();

    internal MarkdownTableBuilder AddColumn(string header, ColumnAlign align = ColumnAlign.Left)
    {
        _columns.Add((header, align));
        return this;
    }

    internal MarkdownTableBuilder AddRow(params object?[] cells)
    {
        var row = new string[_columns.Count];
        for (var i = 0; i < _columns.Count; i++)
        {
            var raw = i < cells.Length ? cells[i]?.ToString() ?? string.Empty : string.Empty;
            row[i] = EscapeCell(raw);
        }
        _rows.Add(row);
        return this;
    }

    internal string BuildHeaderLine()
    {
        var parts = new string[_columns.Count];
        for (var i = 0; i < _columns.Count; i++)
        {
            parts[i] = EscapeCell(_columns[i].Header);
        }
        return "| " + string.Join(" | ", parts) + " |";
    }

    internal string BuildSeparatorLine()
    {
        var sb = new StringBuilder();
        sb.Append('|');
        foreach (var (_, align) in _columns)
        {
            sb.Append(align switch
            {
                ColumnAlign.Right => "---:|",
                ColumnAlign.Center => ":---:|",
                _ => ":---|",
            });
        }
        return sb.ToString();
    }

    internal string BuildRowLine(params object?[] cells)
    {
        var escaped = new string[_columns.Count];
        for (var i = 0; i < _columns.Count; i++)
        {
            var raw = i < cells.Length ? cells[i]?.ToString() ?? string.Empty : string.Empty;
            escaped[i] = EscapeCell(raw);
        }
        return FormatRow(escaped);
    }

    private static string FormatRow(string[] escapedCells)
    {
        return "| " + string.Join(" | ", escapedCells) + " |";
    }

    internal void AppendTo(StringBuilder sb)
    {
        if (_columns.Count == 0) return;
        sb.Append(BuildHeaderLine()).Append('\n');
        sb.Append(BuildSeparatorLine()).Append('\n');
        foreach (var row in _rows)
        {
            sb.Append(FormatRow(row)).Append('\n');
        }
    }

    internal string Build()
    {
        var sb = new StringBuilder();
        AppendTo(sb);
        return sb.ToString();
    }

    internal static string EscapeCell(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "-";
        return text.Replace("\r", string.Empty).Replace("\n", " ").Replace("|", "\\|").Trim();
    }
}

internal sealed class MarkdownBuilder
{
    private readonly StringBuilder _sb = new();

    internal MarkdownBuilder Heading(int level, string text)
    {
        _sb.Append(new string('#', level)).Append(' ').Append(text).Append('\n');
        return this;
    }

    internal MarkdownBuilder BlankLine()
    {
        _sb.Append('\n');
        return this;
    }

    internal MarkdownBuilder Line(string text)
    {
        _sb.Append(text).Append('\n');
        return this;
    }

    internal MarkdownBuilder Divider() =>
        BlankLine().Line("---").BlankLine();

    internal MarkdownBuilder BulletList(IEnumerable<string> items)
    {
        foreach (var item in items)
        {
            _sb.Append("- ").Append(item).Append('\n');
        }
        return this;
    }

    internal MarkdownBuilder CodeBlock(string language, string content)
    {
        _sb.Append("```").Append(language).Append('\n');
        _sb.Append(content);
        if (content.Length > 0 && content[^1] != '\n')
        {
            _sb.Append('\n');
        }
        _sb.Append("```\n");
        return this;
    }

    internal MarkdownBuilder Table(Action<MarkdownTableBuilder> configure)
    {
        var table = new MarkdownTableBuilder();
        configure(table);
        table.AppendTo(_sb);
        return this;
    }

    internal MarkdownBuilder Table(MarkdownTableBuilder instance)
    {
        instance.AppendTo(_sb);
        return this;
    }

    internal void AppendTo(StringBuilder sb) => sb.Append(_sb);

    internal string Build() => _sb.ToString();
}
