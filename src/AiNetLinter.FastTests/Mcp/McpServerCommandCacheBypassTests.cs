#nullable enable

using System;
using System.Linq;
using System.Reflection;
using AiNetLinter.Cache;
using AiNetLinter.Mcp;
using Xunit;

namespace AiNetLinter.FastTests.Mcp;

/// <summary>
///: Ein MCP-Server + ein gleichzeitiger
/// CLI-Lint-Lauf auf derselben Solution kollidieren nicht. Begruendung: der MCP-Modus
/// umgeht den Disk-Cache (<see cref="AnalysisCacheManager"/>) per Design.
/// Statt eines E2E-Process-Coordination-Tests verifiziert dieser Reflection-Test die
/// strukturelle Eigenschaft: <see cref="McpCodeGraphServer"/> hat KEINEN Verweis auf
/// <see cref="AnalysisCacheManager"/>. Wuerde in einer spaeteren Einheit versehentlich
/// ein Disk-Cache-Backport eingebaut, schlaegt der Test fehl.
///
/// A3-Pfad: wenn in <c>McpCodeGraphServer</c> ein Feld/Property vom Typ
/// <see cref="AnalysisCacheManager"/> hinzugefuegt wird (z. B. <c>private readonly
/// AnalysisCacheManager _cache;</c>), dann findet <c>GetFields</c>/<c>GetProperties</c>
/// diesen Eintrag und der Test schlaegt fehl.
/// </summary>
[Trait("Category", "Unit")]
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
