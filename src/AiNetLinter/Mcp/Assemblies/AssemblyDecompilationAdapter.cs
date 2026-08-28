#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyDecompilationAdapter
{
    internal Task<DecompilationResult> DecompileAsync(
        DecompilationRequest request,
        AssemblyReferenceResolution references)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);
        deadline.CancelAfter(request.Options.EffectiveTimeout);
        try
        {
            var selection = SelectTypes(ReadTopLevelTypes(request.AssemblyPath, deadline.Token), request.Options);
            if (selection.Types.Count == 0)
            {
                return Task.FromResult(new DecompilationResult([], selection.Diagnostics, false));
            }

            var decompiler = CreateDecompiler(request.AssemblyPath, references, deadline.Token);
            var documents = DecompileTypes(decompiler, selection.Types, request.Options, deadline.Token, selection.Diagnostics);
            return Task.FromResult(new DecompilationResult(documents, selection.Diagnostics, selection.Diagnostics.Count == 0));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(new DecompilationResult(
                [],
                    [new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(OperationCanceledException)), "Die Decompilation wurde wegen Cancellation oder Deadline abgebrochen.", "error")],
                false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException or ICSharpCode.Decompiler.DecompilerException)
        {
            return Task.FromResult(new DecompilationResult(
                [],
                [new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions)), $"Decompilation fehlgeschlagen: {ex.Message}", "error")],
                false));
        }
    }

    private static List<DecompiledDocument> DecompileTypes(
        ICSharpCode.Decompiler.CSharp.CSharpDecompiler decompiler,
        IReadOnlyList<TypeDefinitionInfo> types,
        AssemblyDecompilationOptions options,
        CancellationToken cancellationToken,
        ICollection<AssemblySessionDiagnostic> diagnostics)
    {
        var documents = new List<DecompiledDocument>(types.Count);
        foreach (var type in types)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = decompiler.DecompileTypesAsString([type.Handle]);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(source))
                {
                    diagnostics.Add(new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.CSharpSource)), $"Typ '{type.MetadataName}' erzeugte keinen Quelltext."));
                    continue;
                }

                source = RemoveCompilerGeneratedNestedTypes(source);
                source = RemoveCompilerGeneratedStateMachineAttributes(source);

                if (source.Length > options.MaxDocumentCharacters)
                {
                    diagnostics.Add(new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxDocumentCharacters)), $"Der dekompilierte Typ '{type.MetadataName}' überschreitet die Dokumentgrenze."));
                    continue;
                }

                var syntaxDiagnostics = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source)
                    .GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    .Take(3)
                    .ToList();
                if (syntaxDiagnostics.Count > 0)
                {
                    diagnostics.Add(new(
                        AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.GeneratedPath)),
                        $"Typ '{type.MetadataName}' erzeugte nicht parsbaren C#-Quelltext: {string.Join("; ", syntaxDiagnostics.Select(diagnostic => diagnostic.Id + " " + diagnostic.GetMessage()))}."));
                    continue;
                }

                documents.Add(new DecompiledDocument(
                    $"source/{documents.Count:D5}-{AssemblyDecompilationCache.SanitizeFileName(type.MetadataName)}.cs",
                    type.MetadataName,
                    source,
                    $"0x{MetadataTokens.GetToken(type.Handle):X8}"));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or ICSharpCode.Decompiler.DecompilerException)
            {
                diagnostics.Add(new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(DecompiledDocument.GeneratedPath)), $"Typ '{type.MetadataName}' konnte nicht dekompiliert werden: {ex.Message}"));
            }
        }

        return documents;
    }

    private static string RemoveCompilerGeneratedNestedTypes(string source)
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
                || !previousLine.Contains("CompilerGenerated", StringComparison.Ordinal))
            {
                break;
            }

            attributeStart = previousLineStart;
        }

        return attributeStart;
    }

    private static string RemoveCompilerGeneratedStateMachineAttributes(string source) =>
        string.Join(
            Environment.NewLine,
            source.Split(Environment.NewLine)
                .Where(line => !line.Contains("[AsyncStateMachine(", StringComparison.Ordinal)
                    && !line.Contains("[IteratorStateMachine(", StringComparison.Ordinal)));

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

    private static ICSharpCode.Decompiler.CSharp.CSharpDecompiler CreateDecompiler(
        string assemblyPath,
        AssemblyReferenceResolution references,
        CancellationToken cancellationToken)
    {
        var settings = new ICSharpCode.Decompiler.DecompilerSettings
        {
            DecompileMemberBodies = false,
            ShowXmlDocumentation = false,
            UseDebugSymbols = false,
            RequiredMembers = false,
            AsyncAwait = true,
            AsyncEnumerator = true,
            AnonymousMethods = true,
            AnonymousTypes = true,
            LocalFunctions = true,
            YieldReturn = true,
        };
        return new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(assemblyPath, references.DecompilerResolver, settings)
        {
            CancellationToken = cancellationToken,
        };
    }

    private static IReadOnlyList<TypeDefinitionInfo> ReadTopLevelTypes(
        string assemblyPath,
        CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        if (!peReader.HasMetadata) return [];
        var reader = peReader.GetMetadataReader();
        var result = new List<TypeDefinitionInfo>();
        foreach (var handle in reader.TypeDefinitions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var definition = reader.GetTypeDefinition(handle);
            if (!definition.GetDeclaringType().IsNil) continue;
            var name = reader.GetString(definition.Name);
            if (name == "<Module>" || name.StartsWith("<", StringComparison.Ordinal)) continue;
            var namespaceName = definition.Namespace.IsNil ? string.Empty : reader.GetString(definition.Namespace);
            var metadataName = string.IsNullOrEmpty(namespaceName) ? name : namespaceName + "." + name;
            result.Add(ReadTypeTree(reader, handle, metadataName, cancellationToken));
        }

        return result;
    }

    private static TypeDefinitionInfo ReadTypeTree(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        string metadataName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var definition = reader.GetTypeDefinition(handle);
        var children = definition.GetNestedTypes()
            .Select(nestedHandle =>
            {
                var nested = reader.GetTypeDefinition(nestedHandle);
                return ReadTypeTree(reader, nestedHandle, metadataName + "." + reader.GetString(nested.Name), cancellationToken);
            })
            .ToList();
        var ownMemberCount = CountMembers(definition);
        return new TypeDefinitionInfo(
            handle,
            metadataName,
            1 + children.Sum(child => child.TypeCount),
            ownMemberCount + children.Sum(child => child.MemberCount),
            1 + ownMemberCount + children.Sum(child => child.ComplexityCost));
    }

    private static int CountMembers(TypeDefinition definition) =>
        definition.GetMethods().Count()
        + definition.GetFields().Count()
        + definition.GetProperties().Count()
        + definition.GetEvents().Count();

    private static TypeSelection SelectTypes(
        IReadOnlyList<TypeDefinitionInfo> types,
        AssemblyDecompilationOptions options)
    {
        var diagnostics = new List<AssemblySessionDiagnostic>();
        var selected = new List<TypeDefinitionInfo>();
        var typeBudget = options.MaxTypes;
        var memberBudget = options.MaxMembers;
        var complexityBudget = options.MaxComplexity;
        foreach (var type in types)
        {
            var rejection = GetLimitDiagnostic(type, typeBudget, memberBudget, complexityBudget);
            if (rejection is not null)
            {
                diagnostics.Add(rejection);
                continue;
            }

            selected.Add(type);
            typeBudget -= type.TypeCount;
            memberBudget -= type.MemberCount;
            complexityBudget -= type.ComplexityCost;
        }

        return new TypeSelection(selected, diagnostics);
    }

    private static AssemblySessionDiagnostic? GetLimitDiagnostic(
        TypeDefinitionInfo type,
        int typeBudget,
        int memberBudget,
        int complexityBudget)
    {
        if (type.TypeCount > typeBudget)
        {
            return new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxTypes)), $"Typbaum '{type.MetadataName}' benötigt {type.TypeCount} von {typeBudget} verbleibenden Typbudgets.");
        }

        if (type.MemberCount > memberBudget)
        {
            return new(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxMembers)), $"Typbaum '{type.MetadataName}' benötigt {type.MemberCount} von {memberBudget} verbleibenden Memberbudgets.");
        }

        return type.ComplexityCost > complexityBudget
            ? new AssemblySessionDiagnostic(AssemblyDiagnosticCodes.For(nameof(AssemblyDecompilationAdapter), nameof(AssemblyDecompilationOptions.MaxComplexity)), $"Typbaum '{type.MetadataName}' benötigt Komplexitätskosten {type.ComplexityCost} von {complexityBudget} verbleibenden Kosten.")
            : null;
    }

    private sealed record TypeSelection(
        IReadOnlyList<TypeDefinitionInfo> Types,
        List<AssemblySessionDiagnostic> Diagnostics);

    private sealed record TypeDefinitionInfo(
        TypeDefinitionHandle Handle,
        string MetadataName,
        int TypeCount,
        int MemberCount,
        int ComplexityCost);
}
