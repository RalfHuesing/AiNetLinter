#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiNetLinter.Mcp.Tools.AssemblyAnalysis;

namespace AiNetLinter.Mcp.Assemblies;

internal sealed class AssemblyDecompilationManifestJsonConverter : JsonConverter<AssemblyDecompilationManifest>
{
    private static readonly string[] ManifestProperties =
    [
        "cacheKey", "canonicalPath", "originalPath", "length", "mtimeUtc", "sha256",
        "assemblyIdentity", "references", "decompilerVersion", "optionsIdentity",
        "cacheSchemaVersion", "generatedFiles", "encoding", "warnings", "errors",
        "unresolvedReferences", "createdUtc", "lastAccessUtc", "status", "complete",
    ];

    public override AssemblyDecompilationManifest Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var properties = ReadProperties(document.RootElement, ManifestProperties, "Manifest");
        return new AssemblyDecompilationManifest
        {
            Input = new AssemblyManifestInput
            {
                CacheKey = ReadString(properties, "cacheKey"),
                CanonicalPath = ReadString(properties, "canonicalPath"),
                OriginalPath = ReadString(properties, "originalPath"),
                Length = ReadInt64(properties, "length"),
                MtimeUtc = ReadDateTime(properties, "mtimeUtc"),
                Sha256 = ReadString(properties, "sha256"),
            },
            References = new AssemblyManifestReferences
            {
                AssemblyIdentity = ReadIdentity(properties, "assemblyIdentity"),
                References = ReadReferences(properties, "references"),
            },
            Format = new AssemblyManifestFormat
            {
                DecompilerVersion = ReadString(properties, "decompilerVersion"),
                OptionsIdentity = ReadString(properties, "optionsIdentity"),
                CacheSchemaVersion = ReadString(properties, "cacheSchemaVersion"),
                GeneratedFiles = ReadStringArray(properties, "generatedFiles"),
                Encoding = ReadString(properties, "encoding"),
            },
            Diagnostics = new AssemblyManifestDiagnostics
            {
                Warnings = ReadStringArray(properties, "warnings"),
                Errors = ReadStringArray(properties, "errors"),
                UnresolvedReferences = ReadStringArray(properties, "unresolvedReferences"),
            },
            Status = new AssemblyManifestStatus
            {
                CreatedUtc = ReadDateTime(properties, "createdUtc"),
                LastAccessUtc = ReadDateTime(properties, "lastAccessUtc"),
                Status = ReadString(properties, "status"),
                Complete = ReadBoolean(properties, "complete"),
            },
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        AssemblyDecompilationManifest value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("cacheKey", value.Input.CacheKey);
        writer.WriteString("canonicalPath", value.Input.CanonicalPath);
        writer.WriteString("originalPath", value.Input.OriginalPath);
        writer.WriteNumber("length", value.Input.Length);
        writer.WriteString("mtimeUtc", value.Input.MtimeUtc);
        writer.WriteString("sha256", value.Input.Sha256);
        WriteIdentity(writer, "assemblyIdentity", value.References.AssemblyIdentity);
        WriteReferences(writer, value.References.References);
        writer.WriteString("decompilerVersion", value.Format.DecompilerVersion);
        writer.WriteString("optionsIdentity", value.Format.OptionsIdentity);
        writer.WriteString("cacheSchemaVersion", value.Format.CacheSchemaVersion);
        WriteStrings(writer, "generatedFiles", value.Format.GeneratedFiles);
        writer.WriteString("encoding", value.Format.Encoding);
        WriteStrings(writer, "warnings", value.Diagnostics.Warnings);
        WriteStrings(writer, "errors", value.Diagnostics.Errors);
        WriteStrings(writer, "unresolvedReferences", value.Diagnostics.UnresolvedReferences);
        writer.WriteString("createdUtc", value.Status.CreatedUtc);
        writer.WriteString("lastAccessUtc", value.Status.LastAccessUtc);
        writer.WriteString("status", value.Status.Status);
        writer.WriteBoolean("complete", value.Status.Complete);
        writer.WriteEndObject();
    }

    private static Dictionary<string, JsonElement> ReadProperties(
        JsonElement root,
        IReadOnlyList<string> expected,
        string description)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"{description} muss ein JSON-Objekt sein.");
        }

        var allowed = new HashSet<string>(expected, StringComparer.Ordinal);
        var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new JsonException($"Unbekanntes {description}-Feld '{property.Name}'.");
            }

            if (!properties.TryAdd(property.Name, property.Value))
            {
                throw new JsonException($"Das {description}-Feld '{property.Name}' ist doppelt vorhanden.");
            }
        }

        foreach (var property in expected)
        {
            if (!properties.ContainsKey(property))
            {
                throw new JsonException($"Das Pflichtfeld '{property}' fehlt im {description}.");
            }
        }

        return properties;
    }

    private static string ReadString(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss ein String sein.");
        }

        return value.GetString() ?? throw new JsonException($"Das Manifestfeld '{name}' darf nicht null sein.");
    }

    private static long ReadInt64(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss eine ganze Zahl sein.");
        }

        return number;
    }

    private static DateTime ReadDateTime(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.String || !value.TryGetDateTime(out var dateTime))
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss ein ISO-Datum sein.");
        }

        return dateTime;
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, JsonElement> properties, string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss Boolean sein.");
        }

        return value.GetBoolean();
    }

    private static IReadOnlyList<string> ReadStringArray(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss ein String-Array sein.");
        }

        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new JsonException($"Das Manifestfeld '{name}' enthält einen nicht typisierten Wert.");
            }

            result.Add(item.GetString() ?? throw new JsonException($"Das Manifestfeld '{name}' enthält null."));
        }

        return result;
    }

    private static AssemblyIdentityDto? ReadIdentity(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        var value = properties[name];
        if (value.ValueKind == JsonValueKind.Null) return null;
        var identity = ReadProperties(value, ["name", "version", "culture", "publicKeyToken"], "Assembly-Identität");
        return new AssemblyIdentityDto(
            ReadString(identity, "name"),
            ReadString(identity, "version"),
            ReadString(identity, "culture"),
            ReadString(identity, "publicKeyToken"));
    }

    private static IReadOnlyList<AssemblyReferenceDto> ReadReferences(
        IReadOnlyDictionary<string, JsonElement> properties,
        string name)
    {
        var value = properties[name];
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Das Manifestfeld '{name}' muss ein Referenz-Array sein.");
        }

        var result = new List<AssemblyReferenceDto>();
        foreach (var item in value.EnumerateArray())
        {
            var reference = ReadProperties(item, ["name", "version", "culture", "resolved", "resolvedPath"], "Assembly-Referenz");
            var resolvedPath = reference["resolvedPath"].ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => reference["resolvedPath"].GetString(),
                _ => throw new JsonException("Das Referenzfeld 'resolvedPath' muss ein String oder null sein."),
            };
            result.Add(new AssemblyReferenceDto(
                ReadString(reference, "name"),
                ReadString(reference, "version"),
                ReadString(reference, "culture"),
                ReadBoolean(reference, "resolved"),
                resolvedPath));
        }

        return result;
    }

    private static void WriteIdentity(Utf8JsonWriter writer, string name, AssemblyIdentityDto? identity)
    {
        writer.WritePropertyName(name);
        if (identity is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        writer.WriteString("name", identity.Name);
        writer.WriteString("version", identity.Version);
        writer.WriteString("culture", identity.Culture);
        writer.WriteString("publicKeyToken", identity.PublicKeyToken);
        writer.WriteEndObject();
    }

    private static void WriteReferences(Utf8JsonWriter writer, IReadOnlyList<AssemblyReferenceDto> references)
    {
        writer.WriteStartArray("references");
        foreach (var reference in references)
        {
            writer.WriteStartObject();
            writer.WriteString("name", reference.Name);
            writer.WriteString("version", reference.Version);
            writer.WriteString("culture", reference.Culture);
            writer.WriteBoolean("resolved", reference.Resolved);
            if (reference.ResolvedPath is null) writer.WriteNull("resolvedPath");
            else writer.WriteString("resolvedPath", reference.ResolvedPath);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IReadOnlyList<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
}
