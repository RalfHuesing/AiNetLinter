#nullable enable

using System;
using System.Text.Json;

namespace AiNetLinter.Configuration;

internal static class ExternalSourceResourceOptionsLoader
{
    private const string MaxDiskBytesName = "MaxDiskBytes";
    private const string MaxMemoryBytesName = "MaxMemoryBytes";
    private const string MaxParallelOperationsName = "MaxParallelOperations";
    private const string MaxResidentResourcesName = "MaxResidentResources";
    private const string IdleTtlMinutesName = "IdleTtlMinutes";

    internal static readonly string[] AllowedNames =
    [
        MaxDiskBytesName,
        MaxMemoryBytesName,
        MaxParallelOperationsName,
        MaxResidentResourcesName,
        IdleTtlMinutesName,
    ];

    internal static bool TryRead(
        ExternalSourceJsonObjectValidation validation,
        string settingsPath,
        out ExternalSourceResourceOptions? resourceOptions,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        resourceOptions = null;
        diagnostic = null;
        if (!TryReadPositiveLong(
                validation.GetProperty(MaxDiskBytesName),
                settingsPath,
                MaxDiskBytesName,
                ExternalSourceResourceOptions.DefaultMaxDiskBytes,
                out var maxDiskBytes,
                out diagnostic)
            || !TryReadPositiveLong(
                validation.GetProperty(MaxMemoryBytesName),
                settingsPath,
                MaxMemoryBytesName,
                ExternalSourceResourceOptions.DefaultMaxMemoryBytes,
                out var maxMemoryBytes,
                out diagnostic)
            || !TryReadPositiveInt(
                validation.GetProperty(MaxParallelOperationsName),
                settingsPath,
                MaxParallelOperationsName,
                ExternalSourceResourceOptions.DefaultMaxParallelOperations,
                out var maxParallelOperations,
                out diagnostic)
            || !TryReadPositiveInt(
                validation.GetProperty(MaxResidentResourcesName),
                settingsPath,
                MaxResidentResourcesName,
                ExternalSourceResourceOptions.DefaultMaxResidentResources,
                out var maxResidentResources,
                out diagnostic)
            || !TryReadIdleTtl(
                validation.GetProperty(AllowedNames[4]),
                settingsPath,
                out var idleTtl,
                out diagnostic))
        {
            return false;
        }

        resourceOptions = new ExternalSourceResourceOptions(
            maxDiskBytes,
            maxMemoryBytes,
            maxParallelOperations,
            maxResidentResources,
            idleTtl);
        return true;
    }

    private static bool TryReadPositiveLong(
        ExternalSourceJsonPropertyValidation property,
        string settingsPath,
        string propertyName,
        long defaultValue,
        out long value,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        value = defaultValue;
        diagnostic = null;
        if (property.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return true;
        }

        if (property.Value.ValueKind is JsonValueKind.Number
            && IsIntegralJsonNumber(property.Value)
            && property.Value.TryGetInt64(out var parsed)
            && parsed > 0
            && parsed <= ExternalSourceResourceOptions.MaxConfiguredBytes)
        {
            value = parsed;
            return true;
        }

        diagnostic = CreateResourceLimitDiagnostic(settingsPath, propertyName);
        return false;
    }

    private static bool TryReadPositiveInt(
        ExternalSourceJsonPropertyValidation property,
        string settingsPath,
        string propertyName,
        int defaultValue,
        out int value,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        value = defaultValue;
        diagnostic = null;
        if (property.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return true;
        }

        if (property.Value.ValueKind is JsonValueKind.Number
            && IsIntegralJsonNumber(property.Value)
            && property.Value.TryGetInt32(out var parsed)
            && parsed > 0)
        {
            value = parsed;
            return true;
        }

        diagnostic = CreateResourceLimitDiagnostic(settingsPath, propertyName);
        return false;
    }

    private static bool TryReadIdleTtl(
        ExternalSourceJsonPropertyValidation property,
        string settingsPath,
        out TimeSpan value,
        out ExternalSourceConfigurationDiagnostic? diagnostic)
    {
        value = ExternalSourceResourceOptions.DefaultIdleTtl;
        diagnostic = null;
        if (property.Status is ExternalSourceJsonPropertyStatus.Missing)
        {
            return true;
        }

        if (property.Value.ValueKind is JsonValueKind.Number
            && property.Value.TryGetDecimal(out var minutes)
            && minutes > 0
            && minutes <= (decimal)TimeSpan.MaxValue.TotalMinutes)
        {
            var ticks = decimal.ToInt64(minutes * TimeSpan.TicksPerMinute);
            if (ticks > 0)
            {
                value = TimeSpan.FromTicks(ticks);
                return true;
            }
        }

        diagnostic = CreateResourceLimitDiagnostic(settingsPath, IdleTtlMinutesName);
        return false;
    }

    private static ExternalSourceConfigurationDiagnostic CreateResourceLimitDiagnostic(
        string settingsPath,
        string propertyName) =>
        ExternalSourceConfigurationDiagnostic.CreateError(
            ExternalSourceConfigurationDiagnosticCodes.ResourceLimitInvalid,
            $"'ExternalSources:{propertyName}' muss einen positiven, endlichen Wert im zulässigen Bereich enthalten.",
            settingsPath,
            $"$.ExternalSources.{propertyName}");

    private static bool IsIntegralJsonNumber(JsonElement value)
    {
        var rawValue = value.GetRawText();
        return rawValue.IndexOfAny(['.', 'e', 'E']) < 0;
    }
}
