#nullable enable

using System.Threading;
using System.Threading.Tasks;
using AiNetLinter.FastTests.Fixtures;
using AiNetLinter.Mcp.Tools.MagicValues;
using Xunit;

namespace AiNetLinter.FastTests.Mcp.Tools.FindMagicValues;

/// <summary>
/// Malfunction-Tests fuer <see cref="FindMagicValuesScanner"/> â€” prueft, dass ein
/// unleserliches Document korrekt als echte Malfunction (IsMalfunction=true mit
/// Context) gemeldet wird, statt stillschweigend ein leeres Ergebnis zu liefern.
/// Aus <see cref="FindMagicValuesScannerTests"/> in eine eigene Datei extrahiert, damit
/// die Haupt-Testklasse unter dem <c>MaxLineCount: 500</c>-Limit bleibt (siehe
/// <c>AiNetLinter.mdc</c>).
/// </summary>
[Trait("Category", "Component")]
public sealed class FindMagicValuesScannerMalfunctionTests
{
    [Fact]
    public async Task ScanAsync_FaultingSolution_ReturnsMalfunctionWithContext()
    {
        using var faulty = new FaultingSolutionFixture();
        var solution = faulty.Solution;

        var result = await FindMagicValuesScanner.ScanAsync(new FindMagicValuesScannerParameters(
            Solution: solution,
            ScopeFilter: null,
            ValueType: MagicValueValueType.String,
            Category: null,
            MinOccurrences: 1,
            MaxResults: 50,
            IgnoreNumbers: null,
            IncludeTests: false,
            IncludeSuppressed: false,
            ChangedOnly: false,
            CancellationToken: CancellationToken.None));

        Assert.True(result.IsMalfunction);
        Assert.Null(result.Payload);
        Assert.NotNull(result.Context);
    }
}

