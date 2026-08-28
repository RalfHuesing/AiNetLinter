#nullable enable

using System.Text;
using AiNetLinter.Output;
using Xunit;

namespace AiNetLinter.FastTests.Output;

[Trait("Category", "Unit")]
public sealed class MarkdownBuilderTests
{
    [Fact]
    public void EscapeCell_Pipe_WirdEscaped()
    {
        var result = MarkdownTableBuilder.EscapeCell("int | string");

        Assert.Equal(@"int \| string", result);
    }

    [Fact]
    public void EscapeCell_Zeilenumbruch_WirdZuSpace()
    {
        var result = MarkdownTableBuilder.EscapeCell("foo\r\nbar\nbaz");

        Assert.Equal("foo bar baz", result);
    }

    [Fact]
    public void EscapeCell_LeerOderWhitespace_WirdMinus()
    {
        Assert.Equal("-", MarkdownTableBuilder.EscapeCell(""));
        Assert.Equal("-", MarkdownTableBuilder.EscapeCell("   "));
        Assert.Equal("-", MarkdownTableBuilder.EscapeCell("\t"));
        Assert.Equal("-", MarkdownTableBuilder.EscapeCell(null));
    }

    [Fact]
    public void EscapeCell_Generics_KeineAenderung()
    {
        Assert.Equal("List<int>", MarkdownTableBuilder.EscapeCell("List<int>"));
        Assert.Equal("IDictionary<string, List<int>>", MarkdownTableBuilder.EscapeCell("IDictionary<string, List<int>>"));
    }

    [Fact]
    public void EscapeCell_BoldUndBackticks_KeineAenderung()
    {
        Assert.Equal("**bold**", MarkdownTableBuilder.EscapeCell("**bold**"));
        Assert.Equal("`code`", MarkdownTableBuilder.EscapeCell("`code`"));
    }

    [Fact]
    public void AlignmentRow_LeftRightCenter_KorrekteSeparatoren()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("L")
            .AddColumn("R", ColumnAlign.Right)
            .AddColumn("C", ColumnAlign.Center)
            .AddRow("a", "b", "c");

        var output = table.Build();

        Assert.Contains("|:---|---:|:---:|", output);
    }

    [Fact]
    public void AddRow_ZuWenigCells_FehlendeWerdenMinus()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C")
            .AddRow("nur", "eins");

        var output = table.Build();

        Assert.Contains("| nur | eins | - |", output);
    }

    [Fact]
    public void AddRow_ZuVieleCells_UeberschuessigeWerdenIgnoriert()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("A")
            .AddColumn("B")
            .AddRow("eins", "zwei", "drei", "vier");

        var output = table.Build();

        Assert.Contains("| eins | zwei |", output);
        Assert.DoesNotContain("drei", output);
        Assert.DoesNotContain("vier", output);
    }

    [Fact]
    public void EscapeCell_MehrerePipes_WerdenAlleEscaped()
    {
        var result = MarkdownTableBuilder.EscapeCell("a | b | c | d");

        Assert.Equal(@"a \| b \| c \| d", result);
    }

    [Fact]
    public void AppendTo_OhneColumns_SchreibtNichts()
    {
        var sb = new StringBuilder();
        new MarkdownTableBuilder().AppendTo(sb);

        Assert.Equal(string.Empty, sb.ToString());
    }

    [Fact]
    public void Build_GibtVolleTabelleAlsString()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("Name")
            .AddColumn("Alter", ColumnAlign.Right)
            .AddRow("Alice", 30)
            .AddRow("Bob", 25);

        var output = table.Build();

        Assert.Contains("| Name | Alter |", output);
        Assert.Contains("|:---|---:|", output);
        Assert.Contains("| Alice | 30 |", output);
        Assert.Contains("| Bob | 25 |", output);
    }

    [Fact]
    public void VollstaendigeTabelle_SnapshotDesOutputs()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("Kind")
            .AddColumn("Name")
            .AddColumn("Lines", ColumnAlign.Right)
            .AddRow("Method", "Foo", "10-20")
            .AddRow("Property", "Bar", "21-25");

        var output = table.Build();

        var expected =
            "| Kind | Name | Lines |\n" +
            "|:---|:---|---:|\n" +
            "| Method | Foo | 10-20 |\n" +
            "| Property | Bar | 21-25 |\n";
        Assert.Equal(expected, output);
    }

    [Fact]
    public void Heading1Und3_KorrektePraefixe()
    {
        var mb = new MarkdownBuilder();

        var output = mb.Heading(1, "Titel").Heading(3, "Sub").Build();

        Assert.Contains("# Titel", output);
        Assert.Contains("### Sub", output);
    }

    [Fact]
    public void CodeBlock_MitUndOhneTrailingNewline()
    {
        var mb = new MarkdownBuilder();

        var output = mb.CodeBlock("csharp", "var x = 1;").Build();

        Assert.Contains("```csharp\nvar x = 1;\n```", output);
    }

    [Fact]
    public void CodeBlock_MitTrailingNewline_KeineDoppelteNewline()
    {
        var mb = new MarkdownBuilder();

        var output = mb.CodeBlock("csharp", "var x = 1;\n").Build();

        Assert.Contains("```csharp\nvar x = 1;\n```", output);
        Assert.DoesNotContain("\n\n```", output);
    }

    [Fact]
    public void CodeBlock_MitTruncationMarker_BleibtSichtbar()
    {
        var mb = new MarkdownBuilder();

        var output = mb.CodeBlock("csharp", "// ⚠ truncated, maxBodyLines erhoehen").Build();

        Assert.Contains("// ⚠ truncated, maxBodyLines erhoehen", output);
    }

    [Fact]
    public void BlankLine_ErzeugtLeereZeile()
    {
        var mb = new MarkdownBuilder();

        var output = mb.Line("vor").BlankLine().Line("nach").Build();

        Assert.Equal("vor\n\nnach\n", output);
    }

    [Fact]
    public void Divider_ErzeugtMarkdownTrennlinie()
    {
        var output = new MarkdownBuilder().Line("vor").Divider().Line("nach").Build();

        Assert.Equal("vor\n\n---\n\nnach\n", output);
    }

    [Fact]
    public void BulletList_PraefixMinusProElement()
    {
        var mb = new MarkdownBuilder();

        var output = mb.BulletList(new[] { "eins", "zwei", "drei" }).Build();

        Assert.Equal("- eins\n- zwei\n- drei\n", output);
    }

    [Fact]
    public void BulletList_LeereListe_SchreibtNichts()
    {
        var output = new MarkdownBuilder().BulletList(new string[0]).Build();

        Assert.Equal(string.Empty, output);
    }

    [Fact]
    public void TableInstanceUeberladung_GibtOutputInBuilder()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("Key")
            .AddRow("alpha");

        var output = new MarkdownBuilder().Table(table).Build();

        Assert.Contains("| Key |", output);
        Assert.Contains("| alpha |", output);
    }

    [Fact]
    public void TableCallback_und_InstanceUeberladung_GleicherOutput()
    {
        var viaCallback = new MarkdownBuilder()
            .Table(t => t.AddColumn("A").AddRow("1"))
            .Build();

        var table = new MarkdownTableBuilder().AddColumn("A").AddRow("1");
        var viaInstance = new MarkdownBuilder().Table(table).Build();

        Assert.Equal(viaCallback, viaInstance);
        Assert.Contains("| A |", viaInstance);
        Assert.Contains("| 1 |", viaInstance);
    }

    [Fact]
    public void AppendTo_LandetInAeusseremStringBuilder()
    {
        var outer = new StringBuilder();
        outer.AppendLine("vor");

        new MarkdownBuilder()
            .Line("mitte")
            .AppendTo(outer);

        outer.AppendLine("nach");

        var output = outer.ToString();
        Assert.Contains("vor", output);
        Assert.Contains("mitte", output);
        Assert.Contains("nach", output);
    }

    [Fact]
    public void Build_GibtGesamtausgabeAlsString()
    {
        var output = new MarkdownBuilder()
            .Heading(2, "Titel")
            .BlankLine()
            .Line("**bold**")
            .Build();

        Assert.Contains("## Titel", output);
        Assert.Contains("**bold**", output);
    }

    [Fact]
    public void DokumentMix_HeadingBulletsTableLine_Snapshot()
    {
        var output = new MarkdownBuilder()
            .Heading(2, "Titel")
            .BlankLine()
            .BulletList(new[] { "punkt eins", "punkt zwei" })
            .BlankLine()
            .Table(t => t.AddColumn("Key").AddColumn("Val").AddRow("a", "1"))
            .BlankLine()
            .Line("**bold**")
            .Build();

        const string expected =
            "## Titel\n" +
            "\n" +
            "- punkt eins\n" +
            "- punkt zwei\n" +
            "\n" +
            "| Key | Val |\n" +
            "|:---|:---|\n" +
            "| a | 1 |\n" +
            "\n" +
            "**bold**\n";
        Assert.Equal(expected, output);
    }

    [Fact]
    public void BuildHeaderLine_Standardfall_GibtEscapedHeaderMitPipes()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("Spalte A")
            .AddColumn("Spalte B", ColumnAlign.Right);

        var line = table.BuildHeaderLine();

        Assert.Equal("| Spalte A | Spalte B |", line);
    }

    [Fact]
    public void BuildHeaderLine_HeaderMitPipe_WirdEscaped()
    {
        var table = new MarkdownTableBuilder().AddColumn("A | B");

        var line = table.BuildHeaderLine();

        Assert.Equal(@"| A \| B |", line);
    }

    [Fact]
    public void BuildSeparatorLine_LeftRightCenter_KorrekteFormatierung()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("L")
            .AddColumn("R", ColumnAlign.Right)
            .AddColumn("C", ColumnAlign.Center);

        var line = table.BuildSeparatorLine();

        Assert.Equal("|:---|---:|:---:|", line);
    }

    [Fact]
    public void BuildRowLine_Standardfall_GibtEscapedCellsMitPipes()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("A")
            .AddColumn("B");

        var line = table.BuildRowLine("eins | zwei", "drei");

        Assert.Equal(@"| eins \| zwei | drei |", line);
    }

    [Fact]
    public void BuildRowLine_ZuWenigCells_FuellstandMitMinus()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("A")
            .AddColumn("B")
            .AddColumn("C");

        var line = table.BuildRowLine("nur", "eins");

        Assert.Equal("| nur | eins | - |", line);
    }

    [Fact]
    public void BuildRowLine_NullCells_WerdenMinus()
    {
        var table = new MarkdownTableBuilder()
            .AddColumn("A")
            .AddColumn("B");

        var line = table.BuildRowLine(null, "x");

        Assert.Equal("| - | x |", line);
    }

    [Fact]
    public void Heading_AppendTo_AppendsFormattedHeadingToOuterStringBuilder()
    {
        var sb = new StringBuilder();
        new MarkdownBuilder().Heading(1, "Test Heading").AppendTo(sb);

        Assert.Equal("# Test Heading\n", sb.ToString());
    }
}
