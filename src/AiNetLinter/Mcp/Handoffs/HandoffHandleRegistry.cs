#nullable enable

using System;
using System.Collections.Concurrent;
using AiNetLinter.Output;

namespace AiNetLinter.Mcp.Handoffs;

/// <summary>
/// Zentrale Registry für flüchtige Handoff-Handles.
/// Verwaltet die 1:1-Bijektion zwischen internen Handoff-IDs und kurzen externen Opaque-Handles (h:...).
/// </summary>
internal sealed class HandoffHandleRegistry
{
    private static readonly Lazy<HandoffHandleRegistry> DefaultInstance =
        new(() => new HandoffHandleRegistry(HandoffCounterStore.Default));

    private readonly ConcurrentDictionary<string, string> internalToExternal = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> externalToInternal = new(StringComparer.Ordinal);
    private readonly object syncLock = new();
    private readonly IHandoffCounterStore counterStore;

    internal HandoffHandleRegistry() : this(HandoffCounterStore.Default)
    {
    }

    internal HandoffHandleRegistry(IHandoffCounterStore counterStore)
    {
        this.counterStore = counterStore ?? throw new ArgumentNullException(nameof(counterStore));
    }

    internal static HandoffHandleRegistry Default => DefaultInstance.Value;

    internal int Count => internalToExternal.Count;

    /// <summary>
    /// Erzeugt oder liefert ein kompaktes, opaques Handle für eine interne Handoff-ID.
    /// Das Handle ist nur eine technische Adresse. Der umgebende MCP-Text muss Symbolart,
    /// verständlichen Namen bzw. Signatur und bei Bedarf relativen Pfad sowie Position ausgeben.
    /// </summary>
    /// <param name="internalHandoffId">Die bestehende interne s:- oder a:-ID.</param>
    /// <returns>Ein <see cref="Result{T}"/> mit dem externen Handle (z. B. "h:a") oder einem Fehler.</returns>
    internal Result<string> GetOrCreateOpaqueHandleForOutput(string internalHandoffId)
    {
        if (string.IsNullOrEmpty(internalHandoffId))
        {
            return Result<string>.Failure(
                LinterErrorCodes.InvalidArgument,
                "Die interne Handoff-ID darf nicht leer sein.");
        }

        if (internalToExternal.TryGetValue(internalHandoffId, out var existingHandle))
        {
            return Result<string>.Success(existingHandle);
        }

        lock (syncLock)
        {
            if (internalToExternal.TryGetValue(internalHandoffId, out existingHandle))
            {
                return Result<string>.Success(existingHandle);
            }

            var nextCounterResult = counterStore.Next();
            if (!nextCounterResult.IsSuccess)
            {
                return Result<string>.Failure(nextCounterResult.Error!.Value);
            }

            var handle = HandoffCounterAlphabet.FormatHandle(nextCounterResult.Value!);
            internalToExternal[internalHandoffId] = handle;
            externalToInternal[handle] = internalHandoffId;

            return Result<string>.Success(handle);
        }
    }

    /// <summary>
    /// Liefert ein opaques Ausgabe-Handle oder bricht die Ausgabe ab. Interne Handoff-IDs duerfen
    /// niemals als Fallback in MCP-Content gelangen, weil sie Target- und Snapshot-Metadaten tragen.
    /// </summary>
    internal string GetOpaqueHandleForOutputOrThrow(string internalHandoffId)
    {
        var result = GetOrCreateOpaqueHandleForOutput(internalHandoffId);
        if (result.IsSuccess) return result.Value!;

        throw new InvalidOperationException(
            $"Handoff-Ausgabe konnte nicht erzeugt werden ({result.Error!.Value.Code}).");
    }

    /// <summary>
    /// Restauriert ein externes Handoff-Handle zu einer internen Handoff-ID oder reicht semantische Eingaben unverändert durch.
    /// </summary>
    /// <param name="externalHandleOrSemanticInput">Die Benutzereingabe (z. B. "h:a", "M:Foo.Bar", "src/File.cs:10:5").</param>
    /// <returns>Die restaurierte interne ID, die unveränderte semantische Eingabe oder ein strukturierter Fehler.</returns>
    internal Result<string> RestoreInternalHandoffForInput(string externalHandleOrSemanticInput)
    {
        if (string.IsNullOrEmpty(externalHandleOrSemanticInput))
        {
            return Result<string>.Success(externalHandleOrSemanticInput);
        }

        if (HandoffCounterAlphabet.IsWindowsDrivePath(externalHandleOrSemanticInput))
        {
            return Result<string>.Success(externalHandleOrSemanticInput);
        }

        if (externalHandleOrSemanticInput.StartsWith(HandoffCounterAlphabet.HandlePrefix, StringComparison.Ordinal))
        {
            if (!HandoffCounterAlphabet.IsValidHandle(externalHandleOrSemanticInput))
            {
                return Result<string>.Failure(
                    LinterErrorCodes.InvalidHandoff,
                    $"Das Handoff-Handle '{externalHandleOrSemanticInput}' ist syntaktisch ungültig.",
                    hint: "Ein gültiges Handle aus der aktuellen Tool-Antwort verwenden (Format: h:...).");
            }

            if (externalToInternal.TryGetValue(externalHandleOrSemanticInput, out var internalId))
            {
                return Result<string>.Success(internalId);
            }

            return Result<string>.Failure(
                LinterErrorCodes.HandoffUnknown,
                $"Das Handoff-Handle '{externalHandleOrSemanticInput}' ist unbekannt. Der MCP-Host wurde möglicherweise neu gestartet.",
                hint: "Bitte das Symbol über find_symbol, get_file_skeleton oder einen passenden Producer erneut ermitteln.");
        }

        if (SymbolHandoffIdentifier.HasWirePrefix(externalHandleOrSemanticInput))
        {
            return Result<string>.Failure(
                LinterErrorCodes.UnsupportedHandoffFormat,
                "Öffentliche Handoff-IDs im Format 's:' oder 'a:' werden nicht mehr unterstützt.",
                hint: "Bitte das neue 'h:...'-Handle aus einer aktuellen Tool-Antwort kopieren.");
        }

        return Result<string>.Success(externalHandleOrSemanticInput);
    }
}
