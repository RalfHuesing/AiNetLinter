#nullable enable

using System;
using System.IO;
using System.Linq;
using AiNetLinter.Cache;
using Xunit;

namespace AiNetLinter.IntegrationTests.Cache;

/// <summary>
///: Zwei Cache-Loads mit unterschiedlichen
/// Solution-Pfaden muessen unterschiedliche Cache-Filenamen erzeugen. Zwei Cache-Loads
/// mit gleichem Solution-Pfad denselben Hash-Anteil. Das Filename-Pattern ist
/// "{solutionName}-{SHA256(solutionPath + rulesJson)[..8]}-{timestamp}.json".
///
/// Diese Tests beweisen die Isolations-Eigenschaft ueber die neu eingefuehrte
/// <see cref="AnalysisCacheManager.CachePath"/>-Property (internal, fuer Test-Sichtbarkeit).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AnalysisCacheManagerIsolationTests : IDisposable
{
    private readonly TestTempDirectory _tempDir = TestTempDirectory.Create("ainetlinter-cache-iso-");

    public void Dispose() => _tempDir.Dispose();

    [Fact]
    public void Load_DifferentSolutionPaths_ProduceDifferentHashes()
    {
        // A3-Kern: zwei Loesungen mit unterschiedlichem Pfad und gleichem rulesJson
        // muessen unterschiedliche Hash-Anteile im Cache-Filenamen haben. Wuerde das
        // SHA256-Pattern weggelassen, waeren die Hashes identisch (nur solutionName
        // waere unterschiedlich, was bei gleichen solutionName zu Kollisionen fuehrt).
        var solAPath = _tempDir.CreateFile("SolutionA.slnx", "");
        var solBPath = _tempDir.CreateFile("SolutionB.slnx", "");

        var managerA = AnalysisCacheManager.Load(_tempDir, solAPath, "{}", TimeSpan.Zero);
        var managerB = AnalysisCacheManager.Load(_tempDir, solBPath, "{}", TimeSpan.Zero);

        var hashA = ExtractHash(managerA.CachePath);
        var hashB = ExtractHash(managerB.CachePath);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public void Load_SameSolutionPath_ProduceSameHash()
    {
        // Identische Loesung mit identischem rulesJson => identischer Hash-Anteil.
        // Der Timestamp-Teil kann variieren (unterschiedlicher Build-Zeitstempel),
        // aber die ersten 8 Hex-Zeichen (Hash) muessen gleich sein.
        var solPath = _tempDir.CreateFile("MySolution.slnx", "");

        var manager1 = AnalysisCacheManager.Load(_tempDir, solPath, "{\"Rules\":{}}", TimeSpan.Zero);
        var manager2 = AnalysisCacheManager.Load(_tempDir, solPath, "{\"Rules\":{}}", TimeSpan.Zero);

        var hash1 = ExtractHash(manager1.CachePath);
        var hash2 = ExtractHash(manager2.CachePath);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void Load_DifferentRulesJson_ProduceDifferentHashes()
    {
        // A3-Kern: unterschiedlicher rulesJson-Inhalt (bei gleichem Solution-Pfad) muss
        // zu unterschiedlichem Hash fuehren, damit eine geaenderte rules.json einen
        // Cache-Invalidations-Effekt hat (alter Cache mit anderen Regeln wird nicht
        // wiederverwendet). Wuerde der Hash nur aus dem solutionPath gebildet, waere
        // der Cache nach einer rules.json-Aenderung veraltet.
        var solPath = _tempDir.CreateFile("MySolution.slnx", "");

        var managerOld = AnalysisCacheManager.Load(_tempDir, solPath, "{\"Rules\":{}}", TimeSpan.Zero);
        var managerNew = AnalysisCacheManager.Load(_tempDir, solPath, "{\"Rules\":{\"New\":true}}", TimeSpan.Zero);

        var hashOld = ExtractHash(managerOld.CachePath);
        var hashNew = ExtractHash(managerNew.CachePath);

        Assert.NotEqual(hashOld, hashNew);
    }

    [Fact]
    public void Load_SamePathCaseInsensitive_ProduceSameHash()
    {
        // Konzept Z. 619-621: Cache-Filenamen muessen case-insensitive sein, damit
        // "C:\Temp\Solution.slnx" und "c:\temp\solution.slnx" denselben Cache treffen
        // (Windows-Dateisystem ist case-insensitive). Das SHA256-Pattern hasht mit
        // ToLowerInvariant, daher identische Hashes.
        var solPath = _tempDir.CreateFile("MySolution.slnx", "");
        var solPathLower = solPath.ToLowerInvariant();

        var manager1 = AnalysisCacheManager.Load(_tempDir, solPath, "{}", TimeSpan.Zero);
        var manager2 = AnalysisCacheManager.Load(_tempDir, solPathLower, "{}", TimeSpan.Zero);

        var hash1 = ExtractHash(manager1.CachePath);
        var hash2 = ExtractHash(manager2.CachePath);

        Assert.Equal(hash1, hash2);
    }

    private static string ExtractHash(string cacheFilePath)
    {
        // Cache-Filename-Pattern: {solutionName}-{hash8}-{timestamp}.json
        // Wir extrahieren den 8-Hex-Zeichen-Hash-Anteil (zweites Segment).
        var fileName = Path.GetFileNameWithoutExtension(cacheFilePath);
        var segments = fileName.Split('-');
        Assert.True(segments.Length >= 2, $"Cache-Filename hat unerwartetes Format: {fileName}");
        return segments[1];
    }
}
