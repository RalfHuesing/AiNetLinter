#nullable enable

using System;
using System.Linq;
using AiNetLinter.Mcp;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.AssemblyAnalysis;

public sealed partial class AssemblyAnalysisToolTests
{
    [Fact]
    public void AssemblySearchPaging_AdvancesWithinVisibleFilesAndStopsAtScopeEnd()
    {
        var matches = new[]
        {
            CreateSearchMatch("src/A.cs", 1),
            CreateSearchMatch("src/A.cs", 2),
            CreateSearchMatch("src/B.cs", 1),
        };

        var firstPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, null));
        var secondPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, "1"));
        var terminalPage = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 1, 0, 0, null, "2"));

        Assert.True(firstPage.HasMoreVisibleMatches);
        Assert.Equal(1, firstPage.NextOffset);
        Assert.True(secondPage.HasMoreVisibleMatches);
        Assert.Equal(2, secondPage.NextOffset);
        Assert.True(terminalPage.MaxFilesTruncated);
        Assert.False(terminalPage.HasMoreVisibleMatches);
        Assert.Equal(2, terminalPage.NextOffset);
    }

    [Fact]
    public void AssemblySearchPaging_SupportsContinuationTokenAsAliasForCursor()
    {
        var matches = new[]
        {
            CreateSearchMatch("src/A.cs", 1),
            CreateSearchMatch("src/A.cs", 2),
            CreateSearchMatch("src/B.cs", 1),
        };

        var secondPageWithCursor = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, Cursor: "1"));

        var secondPageWithContinuationToken = AssemblySearchTool.SelectMatches(
            matches,
            matchedFileCount: 2,
            new AssemblySearchArguments("needle", false, "text", 1, 2, 0, 0, null, Cursor: null, ContinuationToken: "1"));

        Assert.Equal(secondPageWithCursor.NextOffset, secondPageWithContinuationToken.NextOffset);
        Assert.Equal(secondPageWithCursor.VisibleMatches.Single().Id, secondPageWithContinuationToken.VisibleMatches.Single().Id);
    }

    [Fact]
    public void FileFilter_SupportsGlobPatternsAndNegation()
    {
        var csFilter = AssemblyFileFilter.Create("*.cs", "fileFilter");
        Assert.NotNull(csFilter);
        Assert.True(csFilter.IsMatch("src/Services/OrderService.cs"));
        Assert.True(csFilter.IsMatch("OrderService.cs"));
        Assert.False(csFilter.IsMatch("src/Services/readme.md"));

        var notDesignerFilter = AssemblyFileFilter.Create("!*Designer*", "fileFilter");
        Assert.NotNull(notDesignerFilter);
        Assert.True(notDesignerFilter.IsMatch("src/Services/OrderService.cs"));
        Assert.False(notDesignerFilter.IsMatch("src/UI/Form1.Designer.cs"));

        var serviceFilter = AssemblyFileFilter.Create("*Service*.cs", "fileFilter");
        Assert.NotNull(serviceFilter);
        Assert.True(serviceFilter.IsMatch("Services/OrderService.cs"));
        Assert.False(serviceFilter.IsMatch("Controllers/OrderController.cs"));

        var regexFilter = AssemblyFileFilter.Create(@"(?i)Controller\.cs$", "fileFilter");
        Assert.NotNull(regexFilter);
        Assert.True(regexFilter.IsMatch("Controllers/OrderController.cs"));
        Assert.False(regexFilter.IsMatch("Services/OrderService.cs"));
    }

    [Fact]
    public void DataAccessPattern_MatchesDatabaseApisAndSqlStatements_IgnoresLinq()
    {
        var pattern = AssemblySearchTool.BuiltInPatterns[AssemblySearchTool.DataAccessSearchKind];
        var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Echtes SQL und DB-APIs MÜSSEN matchen
        Assert.True(regex.IsMatch(@"context.Database.SqlQuery<Order>(""SELECT Id, Total FROM Orders"");"));
        Assert.True(regex.IsMatch(@"cmd.ExecuteReader();"));
        Assert.True(regex.IsMatch(@"connection.Execute(""INSERT INTO Users VALUES (1, 'Admin')"");"));
        Assert.True(regex.IsMatch(@"context.SaveChanges();"));
        Assert.True(regex.IsMatch(@"File.ReadAllText(""data.txt"");"));
        Assert.True(regex.IsMatch(@"UPDATE Products SET Price = 10;"));
        Assert.True(regex.IsMatch(@"DELETE FROM TempLog;"));

        // Gewöhnliches LINQ darf NICHT matchen
        Assert.False(regex.IsMatch(@"var names = items.Select(x => x.Name);"));
        Assert.False(regex.IsMatch(@"var filtered = list.Where(x => x.Active).Select(x => x.Id);"));
        Assert.False(regex.IsMatch(@"from p in products where p.Price > 10 select p;"));
    }

    private static AssemblySearchMatch CreateSearchMatch(string filePath, int line) =>
        new(
            $"asm-search:{filePath}:{line}",
            filePath,
            line,
            Array.Empty<AssemblySearchMatchRange>(),
            "needle",
            Array.Empty<string>(),
            Array.Empty<string>());

    [Fact]
    public void AssemblySearch_AutoDetectsRegexAndPromotes()
    {
        using var temp = TestTempDirectory.Create("asm-search-autodetect-");
        var filePath = Path.Combine(temp.DirectoryPath, "Service.cs");
        File.WriteAllText(filePath, "public class OrderService { public void Process() {} }");

        // 1. Eindeutige Regex (\s, \w) ohne IsRegex
        var regexArgs = new AssemblySearchArguments(@"class\s+\w+Service", IsRegex: null, SearchKind: "text", MaxResults: 50, MaxFiles: 0, ContextLines: 0, MaxResponseBytes: 0, FileFilter: null, Cursor: null);
        var regexPayload = AssemblySearchTool.Scan(temp.DirectoryPath, regexArgs, CancellationToken.None);
        Assert.Single(regexPayload.Results);

        // 2. Wildcard (*Service) ohne IsRegex (Auto-Promotion)
        var wildcardArgs = new AssemblySearchArguments("*Service", IsRegex: null, SearchKind: "text", MaxResults: 50, MaxFiles: 0, ContextLines: 0, MaxResponseBytes: 0, FileFilter: null, Cursor: null);
        var wildcardPayload = AssemblySearchTool.Scan(temp.DirectoryPath, wildcardArgs, CancellationToken.None);
        Assert.Single(wildcardPayload.Results);
    }
}
