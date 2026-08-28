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
                [new AssemblySessionDiagnostic("assembly-decompilation-cancelled", "Die Decompilation wurde wegen Cancellation oder Deadline abgebrochen.", "error")],
                false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException or InvalidOperationException or ArgumentException or ICSharpCode.Decompiler.DecompilerException)
        {
            return Task.FromResult(new DecompilationResult(
                [],
                [new AssemblySessionDiagnostic("assembly-decompilation-failed", $"Decompilation fehlgeschlagen: {ex.Message}", "error")],
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
                    diagnostics.Add(new("assembly-type-decompilation-empty", $"Typ '{type.MetadataName}' erzeugte keinen Quelltext."));
                    continue;
                }

                if (source.Length > options.MaxDocumentCharacters)
                {
                    diagnostics.Add(new("assembly-document-size-limit", $"Der dekompilierte Typ '{type.MetadataName}' überschreitet die Dokumentgrenze."));
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
                diagnostics.Add(new("assembly-type-decompilation-failed", $"Typ '{type.MetadataName}' konnte nicht dekompiliert werden: {ex.Message}"));
            }
        }

        return documents;
    }

    private static ICSharpCode.Decompiler.CSharp.CSharpDecompiler CreateDecompiler(
        string assemblyPath,
        AssemblyReferenceResolution references,
        CancellationToken cancellationToken)
    {
        var settings = new ICSharpCode.Decompiler.DecompilerSettings
        {
            DecompileMemberBodies = true,
            ShowXmlDocumentation = false,
            UseDebugSymbols = false,
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
            if (name == "<Module>") continue;
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
            return new("assembly-type-limit", $"Typbaum '{type.MetadataName}' benötigt {type.TypeCount} von {typeBudget} verbleibenden Typbudgets.");
        }

        if (type.MemberCount > memberBudget)
        {
            return new("assembly-member-limit", $"Typbaum '{type.MetadataName}' benötigt {type.MemberCount} von {memberBudget} verbleibenden Memberbudgets.");
        }

        return type.ComplexityCost > complexityBudget
            ? new AssemblySessionDiagnostic("assembly-complexity-limit", $"Typbaum '{type.MetadataName}' benötigt Komplexitätskosten {type.ComplexityCost} von {complexityBudget} verbleibenden Kosten.")
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
