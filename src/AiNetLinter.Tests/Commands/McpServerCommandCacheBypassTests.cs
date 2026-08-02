#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AiNetLinter.Cache;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.Tests.Commands;

public sealed class McpServerCommandCacheBypassTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void McpCodeGraphServer_HasNoAnalysisCacheManagerReference()
    {
        // Kein Feld vom Typ AnalysisCacheManager.
        var cacheFields = typeof(McpCodeGraphServer)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(f => f.FieldType == typeof(AnalysisCacheManager))
            .ToArray();
        Assert.Empty(cacheFields);

        // Keine Property vom Typ AnalysisCacheManager.
        var cacheProperties = typeof(McpCodeGraphServer)
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(AnalysisCacheManager))
            .ToArray();
        Assert.Empty(cacheProperties);

        // Kein Konstruktor-Parameter vom Typ AnalysisCacheManager.
        var cacheCtorParams = typeof(McpCodeGraphServer)
            .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(c => c.GetParameters())
            .Where(p => p.ParameterType == typeof(AnalysisCacheManager))
            .ToArray();
        Assert.Empty(cacheCtorParams);
    }
}
