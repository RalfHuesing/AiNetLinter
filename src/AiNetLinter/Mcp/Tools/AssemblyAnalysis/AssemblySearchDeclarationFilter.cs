#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace AiNetLinter.Mcp.Tools.AssemblyAnalysis;

internal readonly record struct AssemblySyntaxContext(SyntaxNode? Root, SourceText? Text)
{
    internal bool HasValue => Root != null && Text != null;
}

internal static class AssemblySearchDeclarationFilter
{
    internal static AssemblySyntaxContext InitSyntaxTree(
        IReadOnlyList<string> lines,
        AssemblySearchFileParameters options)
    {
        var hasMatch = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (options.Regex.IsMatch(lines[i]))
            {
                hasMatch = true;
                break;
            }
        }

        if (!hasMatch) return default;

        var fullText = string.Join("\n", lines);
        var sourceText = SourceText.From(fullText, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(sourceText, cancellationToken: options.CancellationToken);
        return new AssemblySyntaxContext(tree.GetRoot(options.CancellationToken), sourceText);
    }

    internal static IReadOnlyList<AssemblySearchMatchRange> FilterDeclarationRanges(
        AssemblySyntaxContext context,
        int lineIndex,
        IReadOnlyList<AssemblySearchMatchRange> ranges,
        string? expectedKind)
    {
        if (!context.HasValue) return ranges;

        List<AssemblySearchMatchRange>? filtered = null;
        for (var i = 0; i < ranges.Count; i++)
        {
            if (IsDeclarationMatch(context.Root!, context.Text!, lineIndex, ranges[i].Column, ranges[i].Length, expectedKind))
            {
                filtered ??= new List<AssemblySearchMatchRange>(ranges.Count);
                filtered.Add(ranges[i]);
            }
        }

        return filtered ?? (IReadOnlyList<AssemblySearchMatchRange>)[];
    }

    private static bool IsDeclarationMatch(
        SyntaxNode root,
        SourceText sourceText,
        int lineIndex,
        int column1Based,
        int length,
        string? expectedKind)
    {
        var line = sourceText.Lines[lineIndex];
        var charPosition = line.Start + (column1Based - 1);
        var span = new TextSpan(charPosition, length);

        var trivia = root.FindTrivia(charPosition);
        if (trivia != default && IsCommentOrDocTrivia(trivia))
        {
            return false;
        }

        var token = root.FindToken(charPosition, findInsideTrivia: true);
        if (token.Parent is StructuredTriviaSyntax || IsCommentOrDocToken(token) || IsStringLiteral(token))
        {
            return false;
        }

        var targetToken = root.FindToken(charPosition);
        var declInfo = FindDeclarationHeader(targetToken.Parent);
        if (declInfo == null)
        {
            return false;
        }

        if (span.Start < declInfo.Value.HeaderSpan.Start || span.End > declInfo.Value.HeaderSpan.End)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(expectedKind))
        {
            var normalizedKind = expectedKind.Trim().ToLowerInvariant();
            return string.Equals(declInfo.Value.Kind, normalizedKind, StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private readonly record struct DeclarationHeaderInfo(string Kind, TextSpan HeaderSpan);

    private static DeclarationHeaderInfo? FindDeclarationHeader(SyntaxNode? node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            if (current is BlockSyntax or ArrowExpressionClauseSyntax)
            {
                return null;
            }

            var info = ResolveCallableHeader(current)
                ?? ResolveMemberHeader(current)
                ?? ResolveTypeHeader(current);

            if (info != null)
            {
                return info;
            }
        }

        return null;
    }

    private static DeclarationHeaderInfo? ResolveCallableHeader(SyntaxNode node) =>
        node switch
        {
            MethodDeclarationSyntax method => ResolveMethodHeader(method),
            ConstructorDeclarationSyntax ctor => ResolveCtorHeader(ctor),
            DestructorDeclarationSyntax dtor => ResolveDtorHeader(dtor),
            _ => null
        };

    private static DeclarationHeaderInfo ResolveMethodHeader(MethodDeclarationSyntax method)
    {
        var headerEnd = GetHeaderEnd(method.Body, method.ExpressionBody, method.SemicolonToken, method.SpanStart);
        return new DeclarationHeaderInfo("method", TextSpan.FromBounds(method.SpanStart, headerEnd));
    }

    private static DeclarationHeaderInfo ResolveCtorHeader(ConstructorDeclarationSyntax ctor)
    {
        var headerEnd = GetHeaderEnd(ctor.Body, ctor.ExpressionBody, ctor.SemicolonToken, ctor.SpanStart);
        return new DeclarationHeaderInfo("method", TextSpan.FromBounds(ctor.SpanStart, headerEnd));
    }

    private static DeclarationHeaderInfo ResolveDtorHeader(DestructorDeclarationSyntax dtor)
    {
        var headerEnd = GetHeaderEnd(dtor.Body, dtor.ExpressionBody, dtor.SemicolonToken, dtor.SpanStart);
        return new DeclarationHeaderInfo("method", TextSpan.FromBounds(dtor.SpanStart, headerEnd));
    }

    private static int GetHeaderEnd(BlockSyntax? body, ArrowExpressionClauseSyntax? expressionBody, SyntaxToken semicolon, int spanStart)
    {
        if (body != null) return body.OpenBraceToken.SpanStart;
        if (expressionBody != null) return expressionBody.ArrowToken.SpanStart;
        if (semicolon != default && semicolon.Span.End > spanStart) return semicolon.Span.End;
        return spanStart;
    }

    private static DeclarationHeaderInfo? ResolveMemberHeader(SyntaxNode node) =>
        node switch
        {
            PropertyDeclarationSyntax prop => ResolvePropertyHeader(prop),
            IndexerDeclarationSyntax idx => ResolveIndexerHeader(idx),
            EventDeclarationSyntax evt => ResolveEventHeader(evt),
            FieldDeclarationSyntax field => new DeclarationHeaderInfo("field", field.Span),
            EventFieldDeclarationSyntax evtField => new DeclarationHeaderInfo("event", evtField.Span),
            EnumMemberDeclarationSyntax enumMember => new DeclarationHeaderInfo("enum_member", enumMember.Span),
            _ => null
        };

    private static DeclarationHeaderInfo ResolvePropertyHeader(PropertyDeclarationSyntax prop)
    {
        var headerEnd = prop.AccessorList?.OpenBraceToken.SpanStart
            ?? prop.ExpressionBody?.ArrowToken.SpanStart
            ?? prop.Initializer?.EqualsToken.SpanStart
            ?? prop.SemicolonToken.Span.End;
        if (headerEnd <= prop.SpanStart) headerEnd = prop.Span.End;
        return new DeclarationHeaderInfo("property", TextSpan.FromBounds(prop.SpanStart, headerEnd));
    }

    private static DeclarationHeaderInfo ResolveIndexerHeader(IndexerDeclarationSyntax indexer)
    {
        var headerEnd = indexer.AccessorList?.OpenBraceToken.SpanStart
            ?? indexer.ExpressionBody?.ArrowToken.SpanStart
            ?? indexer.SemicolonToken.Span.End;
        if (headerEnd <= indexer.SpanStart) headerEnd = indexer.Span.End;
        return new DeclarationHeaderInfo("property", TextSpan.FromBounds(indexer.SpanStart, headerEnd));
    }

    private static DeclarationHeaderInfo ResolveEventHeader(EventDeclarationSyntax evt)
    {
        var headerEnd = evt.AccessorList?.OpenBraceToken.SpanStart ?? evt.SemicolonToken.Span.End;
        if (headerEnd <= evt.SpanStart) headerEnd = evt.Span.End;
        return new DeclarationHeaderInfo("event", TextSpan.FromBounds(evt.SpanStart, headerEnd));
    }

    private static DeclarationHeaderInfo? ResolveTypeHeader(SyntaxNode node)
    {
        switch (node)
        {
            case BaseTypeDeclarationSyntax type:
            {
                var headerEnd = type.OpenBraceToken.SpanStart;
                if (headerEnd <= type.SpanStart) headerEnd = type.Span.End;
                return new DeclarationHeaderInfo("type", TextSpan.FromBounds(type.SpanStart, headerEnd));
            }
            case DelegateDeclarationSyntax del:
                return new DeclarationHeaderInfo("type", del.Span);
            default:
                return null;
        }
    }

    private static bool IsCommentOrDocTrivia(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);

    private static bool IsCommentOrDocToken(SyntaxToken token) =>
        token.IsKind(SyntaxKind.XmlTextLiteralToken)
        || token.IsKind(SyntaxKind.XmlTextLiteralNewLineToken)
        || token.Parent is XmlNodeSyntax
        || token.Parent is DocumentationCommentTriviaSyntax;

    private static bool IsStringLiteral(SyntaxToken token) =>
        token.IsKind(SyntaxKind.StringLiteralToken)
        || token.IsKind(SyntaxKind.CharacterLiteralToken)
        || token.IsKind(SyntaxKind.InterpolatedStringTextToken)
        || token.Parent is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression }
        || token.Parent is InterpolatedStringExpressionSyntax;
}
