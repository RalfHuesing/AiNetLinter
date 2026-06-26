#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using AiNetLinter.Baseline;
using AiNetLinter.Output;
using AiNetLinter.Configuration;
using AiNetLinter.Cli;

namespace AiNetLinter.Maps.Skeleton;

/// <summary>
/// Lädt eine Solution via MSBuildWorkspace und erzeugt eine Skeleton Map für LLM-Audits.
/// </summary>
internal static class SkeletonMapBuilder
{
    internal static async Task<int> BuildAsync(
        string targetPath,
        Config config,
        ILintConsole console,
        LinterArgs args,
        CancellationToken ct = default)
    {
        using SourceFileCatalog catalog = await SourceFileCatalog.LoadAsync(targetPath, ct);
        var solutionPath = catalog.Solution.FilePath ?? targetPath;
        var solutionDir = Path.GetDirectoryName(solutionPath) ?? targetPath;

        var types = await ExtractTypesAsync(catalog.Solution, solutionDir, args, config, ct);

        var markdown = SkeletonMarkdownRenderer.Render(types, solutionPath, DateTimeOffset.Now);
        console.WriteLine(markdown);
        return 0;
    }

    private static async Task<IReadOnlyList<SkeletonTypeInfo>> ExtractTypesAsync(
        Solution solution,
        string solutionDir,
        LinterArgs args,
        Config config,
        CancellationToken ct)
    {
        var allTypes = new System.Collections.Concurrent.ConcurrentBag<SkeletonTypeInfo>();
        var documents = CollectDocuments(solution, solutionDir, args, config);

        await Parallel.ForEachAsync(documents, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct,
        }, async (doc, token) =>
        {
            var docTypes = await ExtractFromDocumentAsync(doc, solutionDir, args, token);
            foreach (var t in docTypes)
                allTypes.Add(t);
        });

        return [.. allTypes];
    }

    private static IReadOnlyList<Document> CollectDocuments(Solution solution, string solutionDir, LinterArgs args, Config config)
    {
        return solution.Projects
            .Where(project => SourceFileCatalog.ShouldIncludeProject(project, args, config))
            .SelectMany(p => p.Documents)
            .Where(d => SourceFileCatalog.IsValidDocument(d, solutionDir))
            .ToList();
    }

    private static async Task<IReadOnlyList<SkeletonTypeInfo>> ExtractFromDocumentAsync(
        Document document,
        string solutionDir,
        LinterArgs args,
        CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel == null) return [];

        var relativePath = PathNormalizer.ToRelative(solutionDir, document.FilePath ?? document.Name);
        var walker = new SkeletonSyntaxWalker(
            semanticModel,
            relativePath,
            args.IncludeNamespaces,
            args.ExcludeNamespaces,
            args.PublicOnly);
        var root = await semanticModel.SyntaxTree.GetRootAsync(ct);
        walker.Visit(root);
        return walker.Types;
    }
}
