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
        JsonPropertyName(nameof(AssemblyManifestInput.CacheKey)),
        JsonPropertyName(nameof(AssemblyManifestInput.CanonicalPath)),
        JsonPropertyName(nameof(AssemblyManifestInput.OriginalPath)),
        JsonPropertyName(nameof(AssemblyManifestInput.Length)),
        JsonPropertyName(nameof(AssemblyManifestInput.MtimeUtc)),
        JsonPropertyName(nameof(AssemblyManifestInput.Sha256)),
        JsonPropertyName(nameof(AssemblyManifestReferences.AssemblyIdentity)),
        JsonPropertyName(nameof(AssemblyManifestReferences.References)),
        JsonPropertyName(nameof(AssemblyManifestFormat.DecompilerVersion)),
        JsonPropertyName(nameof(AssemblyManifestFormat.OptionsIdentity)),
        JsonPropertyName(nameof(AssemblyManifestFormat.CacheSchemaVersion)),
        JsonPropertyName(nameof(AssemblyManifestFormat.GeneratedFiles)),
        JsonPropertyName(nameof(AssemblyManifestFormat.Encoding)),
        JsonPropertyName(nameof(AssemblyManifestDiagnostics.Warnings)),
        JsonPropertyName(nameof(AssemblyManifestDiagnostics.Errors)),
        JsonPropertyName(nameof(AssemblyManifestDiagnostics.UnresolvedReferences)),
        JsonPropertyName(nameof(AssemblyManifestStatus.CreatedUtc)),
        JsonPropertyName(nameof(AssemblyManifestStatus.LastAccessUtc)),
        JsonPropertyName(nameof(AssemblyManifestStatus.Status)),
        JsonPropertyName(nameof(AssemblyManifestStatus.Complete)),
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
                CacheKey = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestInput.CacheKey))),
                CanonicalPath = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestInput.CanonicalPath))),
                OriginalPath = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestInput.OriginalPath))),
                Length = ReadInt64(properties, JsonPropertyName(nameof(AssemblyManifestInput.Length))),
                MtimeUtc = ReadDateTime(properties, JsonPropertyName(nameof(AssemblyManifestInput.MtimeUtc))),
                Sha256 = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestInput.Sha256))),
            },
            References = new AssemblyManifestReferences
            {
                AssemblyIdentity = ReadIdentity(properties, JsonPropertyName(nameof(AssemblyManifestReferences.AssemblyIdentity))),
                References = ReadReferences(properties, JsonPropertyName(nameof(AssemblyManifestReferences.References))),
            },
            Format = new AssemblyManifestFormat
            {
                DecompilerVersion = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestFormat.DecompilerVersion))),
                OptionsIdentity = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestFormat.OptionsIdentity))),
                CacheSchemaVersion = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestFormat.CacheSchemaVersion))),
                GeneratedFiles = ReadStringArray(properties, JsonPropertyName(nameof(AssemblyManifestFormat.GeneratedFiles))),
                Encoding = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestFormat.Encoding))),
            },
            Diagnostics = new AssemblyManifestDiagnostics
            {
                Warnings = ReadStringArray(properties, JsonPropertyName(nameof(AssemblyManifestDiagnostics.Warnings))),
                Errors = ReadStringArray(properties, JsonPropertyName(nameof(AssemblyManifestDiagnostics.Errors))),
                UnresolvedReferences = ReadStringArray(properties, JsonPropertyName(nameof(AssemblyManifestDiagnostics.UnresolvedReferences))),
            },
            Status = new AssemblyManifestStatus
            {
                CreatedUtc = ReadDateTime(properties, JsonPropertyName(nameof(AssemblyManifestStatus.CreatedUtc))),
                LastAccessUtc = ReadDateTime(properties, JsonPropertyName(nameof(AssemblyManifestStatus.LastAccessUtc))),
                Status = ReadString(properties, JsonPropertyName(nameof(AssemblyManifestStatus.Status))),
                Complete = ReadBoolean(properties, JsonPropertyName(nameof(AssemblyManifestStatus.Complete))),
            },
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        AssemblyDecompilationManifest value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestInput.CacheKey)), value.Input.CacheKey);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestInput.CanonicalPath)), value.Input.CanonicalPath);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestInput.OriginalPath)), value.Input.OriginalPath);
        writer.WriteNumber(JsonPropertyName(nameof(AssemblyManifestInput.Length)), value.Input.Length);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestInput.MtimeUtc)), value.Input.MtimeUtc);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestInput.Sha256)), value.Input.Sha256);
        WriteIdentity(writer, JsonPropertyName(nameof(AssemblyManifestReferences.AssemblyIdentity)), value.References.AssemblyIdentity);
        WriteReferences(writer, value.References.References);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestFormat.DecompilerVersion)), value.Format.DecompilerVersion);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestFormat.OptionsIdentity)), value.Format.OptionsIdentity);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestFormat.CacheSchemaVersion)), value.Format.CacheSchemaVersion);
        WriteStrings(writer, JsonPropertyName(nameof(AssemblyManifestFormat.GeneratedFiles)), value.Format.GeneratedFiles);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestFormat.Encoding)), value.Format.Encoding);
        WriteStrings(writer, JsonPropertyName(nameof(AssemblyManifestDiagnostics.Warnings)), value.Diagnostics.Warnings);
        WriteStrings(writer, JsonPropertyName(nameof(AssemblyManifestDiagnostics.Errors)), value.Diagnostics.Errors);
        WriteStrings(writer, JsonPropertyName(nameof(AssemblyManifestDiagnostics.UnresolvedReferences)), value.Diagnostics.UnresolvedReferences);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestStatus.CreatedUtc)), value.Status.CreatedUtc);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestStatus.LastAccessUtc)), value.Status.LastAccessUtc);
        writer.WriteString(JsonPropertyName(nameof(AssemblyManifestStatus.Status)), value.Status.Status);
        writer.WriteBoolean(JsonPropertyName(nameof(AssemblyManifestStatus.Complete)), value.Status.Complete);
        writer.WriteEndObject();
    }

    private static string JsonPropertyName(string propertyName) =>
        JsonNamingPolicy.CamelCase.ConvertName(propertyName);

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
        var identity = ReadProperties(
            value,
            [
                JsonPropertyName(nameof(AssemblyIdentityDto.Name)),
                JsonPropertyName(nameof(AssemblyIdentityDto.Version)),
                JsonPropertyName(nameof(AssemblyIdentityDto.Culture)),
                JsonPropertyName(nameof(AssemblyIdentityDto.PublicKeyToken)),
            ],
            "Assembly-Identität");
        return new AssemblyIdentityDto(
            ReadString(identity, JsonPropertyName(nameof(AssemblyIdentityDto.Name))),
            ReadString(identity, JsonPropertyName(nameof(AssemblyIdentityDto.Version))),
            ReadString(identity, JsonPropertyName(nameof(AssemblyIdentityDto.Culture))),
            ReadString(identity, JsonPropertyName(nameof(AssemblyIdentityDto.PublicKeyToken))));
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
            var reference = ReadProperties(
                item,
                [
                    JsonPropertyName(nameof(AssemblyReferenceDto.Name)),
                    JsonPropertyName(nameof(AssemblyReferenceDto.Version)),
                    JsonPropertyName(nameof(AssemblyReferenceDto.Culture)),
                    JsonPropertyName(nameof(AssemblyReferenceDto.Resolved)),
                    JsonPropertyName(nameof(AssemblyReferenceDto.ResolvedPath)),
                ],
                "Assembly-Referenz");
            var resolvedPathName = JsonPropertyName(nameof(AssemblyReferenceDto.ResolvedPath));
            var resolvedPath = reference[resolvedPathName].ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => reference[resolvedPathName].GetString(),
                _ => throw new JsonException($"Das Referenzfeld '{resolvedPathName}' muss ein String oder null sein."),
            };
            result.Add(new AssemblyReferenceDto(
                ReadString(reference, JsonPropertyName(nameof(AssemblyReferenceDto.Name))),
                ReadString(reference, JsonPropertyName(nameof(AssemblyReferenceDto.Version))),
                ReadString(reference, JsonPropertyName(nameof(AssemblyReferenceDto.Culture))),
                ReadBoolean(reference, JsonPropertyName(nameof(AssemblyReferenceDto.Resolved))),
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
        writer.WriteString(JsonPropertyName(nameof(AssemblyIdentityDto.Name)), identity.Name);
        writer.WriteString(JsonPropertyName(nameof(AssemblyIdentityDto.Version)), identity.Version);
        writer.WriteString(JsonPropertyName(nameof(AssemblyIdentityDto.Culture)), identity.Culture);
        writer.WriteString(JsonPropertyName(nameof(AssemblyIdentityDto.PublicKeyToken)), identity.PublicKeyToken);
        writer.WriteEndObject();
    }

    private static void WriteReferences(Utf8JsonWriter writer, IReadOnlyList<AssemblyReferenceDto> references)
    {
        writer.WriteStartArray(JsonPropertyName(nameof(AssemblyManifestReferences.References)));
        foreach (var reference in references)
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyName(nameof(AssemblyReferenceDto.Name)), reference.Name);
            writer.WriteString(JsonPropertyName(nameof(AssemblyReferenceDto.Version)), reference.Version);
            writer.WriteString(JsonPropertyName(nameof(AssemblyReferenceDto.Culture)), reference.Culture);
            writer.WriteBoolean(JsonPropertyName(nameof(AssemblyReferenceDto.Resolved)), reference.Resolved);
            if (reference.ResolvedPath is null)
            {
                writer.WriteNull(JsonPropertyName(nameof(AssemblyReferenceDto.ResolvedPath)));
            }
            else
            {
                writer.WriteString(JsonPropertyName(nameof(AssemblyReferenceDto.ResolvedPath)), reference.ResolvedPath);
            }
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
