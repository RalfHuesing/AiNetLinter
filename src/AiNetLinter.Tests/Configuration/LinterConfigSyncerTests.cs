#nullable enable

using System.IO;
using System.Text.Json;
using AiNetLinter.Configuration;
using Xunit;

namespace AiNetLinter.Tests.Configuration;

public sealed class LinterConfigSyncerTests
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string WriteTempJson(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        File.WriteAllText(path, content);
        return path;
    }

    private static LinterConfig DefaultConfig() => new()
    {
        Global = new GlobalConfig(),
        Metrics = new MetricsConfig(),
    };

    // --- neue Optionen werden ergänzt ---

    [Fact]
    public void SyncIfNeeded_AddsNewOption_WhenMissingFromUserFile()
    {
        // rules.json ohne BanPublicNestedTypes (simuliert "vor Einführung des Keys")
        const string oldJson = """
            {
              "Global": { "EnforceSealedClasses": true },
              "Metrics": { "MaxLineCount": 700 }
            }
            """;
        var path = WriteTempJson(oldJson);
        try
        {
            var config = DefaultConfig();
            var updated = LinterConfigSyncer.SyncIfNeeded(path, config);

            Assert.True(updated);
            var written = File.ReadAllText(path);
            Assert.Contains("BanPublicNestedTypes", written);
        }
        finally { File.Delete(path); }
    }

    // --- veraltete Optionen verschwinden ---

    [Fact]
    public void SyncIfNeeded_RemovesObsoleteOption_WhenKeyNoLongerInSchema()
    {
        const string oldJson = """
            {
              "Global": {
                "EnforceSealedClasses": true,
                "EnforceNoMagicValues": true
              },
              "Metrics": { "MaxLineCount": 700 }
            }
            """;
        var path = WriteTempJson(oldJson);
        try
        {
            var config = DefaultConfig();
            LinterConfigSyncer.SyncIfNeeded(path, config);

            var written = File.ReadAllText(path);
            Assert.DoesNotContain("EnforceNoMagicValues", written);
            Assert.Contains("EnforceSealedClasses", written);
        }
        finally { File.Delete(path); }
    }

    // --- Nutzer-Werte bleiben erhalten ---

    [Fact]
    public void SyncIfNeeded_PreservesUserCustomizations()
    {
        const string userJson = """
            {
              "Global": { "EnforceSealedClasses": false },
              "Metrics": { "MaxLineCount": 500 }
            }
            """;
        var path = WriteTempJson(userJson);
        try
        {
            var loadedConfig = new LinterConfig
            {
                Global = new GlobalConfig { EnforceSealedClasses = false },
                Metrics = new MetricsConfig { MaxLineCount = 500 },
            };
            LinterConfigSyncer.SyncIfNeeded(path, loadedConfig);

            var written = File.ReadAllText(path);
            var reloaded = JsonSerializer.Deserialize<LinterConfig>(written, ReadOptions)!;

            Assert.False(reloaded.Global.EnforceSealedClasses);
            Assert.Equal(500, reloaded.Metrics.MaxLineCount);
        }
        finally { File.Delete(path); }
    }

    // --- ProjectOverrides bleiben sparse ---

    [Fact]
    public void SyncIfNeeded_PreservesProjectOverrides_AsSparse()
    {
        const string userJson = """
            {
              "Global": { "EnforceSealedClasses": true },
              "Metrics": { "MaxLineCount": 700 },
              "ProjectOverrides": {
                "*.Tests": {
                  "Metrics": { "MaxMethodLineCount": 100 },
                  "Global": { "EnforceSealedClasses": false }
                }
              }
            }
            """;
        var path = WriteTempJson(userJson);
        try
        {
            var loadedConfig = new LinterConfig
            {
                Global = new GlobalConfig(),
                Metrics = new MetricsConfig(),
                ProjectOverrides = new Dictionary<string, ProjectOverrideEntry>
                {
                    ["*.Tests"] = new ProjectOverrideEntry
                    {
                        Metrics = new MetricsConfigOverride { MaxMethodLineCount = 100 },
                        Global = new GlobalConfigOverride { EnforceSealedClasses = false },
                    }
                },
            };

            LinterConfigSyncer.SyncIfNeeded(path, loadedConfig);

            var written = File.ReadAllText(path);
            Assert.Contains("*.Tests", written);
            Assert.Contains("MaxMethodLineCount", written);

            // Sparse: andere Override-Felder nicht im Ausgabe-JSON
            Assert.DoesNotContain("MaxLineCount", written.Split("ProjectOverrides")[1]);
        }
        finally { File.Delete(path); }
    }

    // --- kein Schreiben wenn bereits aktuell ---

    [Fact]
    public void SyncIfNeeded_ReturnsFalse_WhenFileAlreadySynced()
    {
        var config = DefaultConfig();
        var syncedJson = LinterConfigSyncer.Serialize(config);
        var path = WriteTempJson(syncedJson);
        try
        {
            var updated = LinterConfigSyncer.SyncIfNeeded(path, config);
            Assert.False(updated);
        }
        finally { File.Delete(path); }
    }

    // --- Datei-Timestamp ändert sich nicht wenn kein Sync nötig ---

    [Fact]
    public void SyncIfNeeded_DoesNotWriteFile_WhenAlreadySynced()
    {
        var config = DefaultConfig();
        var syncedJson = LinterConfigSyncer.Serialize(config);
        var path = WriteTempJson(syncedJson);
        try
        {
            var beforeWrite = File.GetLastWriteTimeUtc(path);
            LinterConfigSyncer.SyncIfNeeded(path, config);
            var afterWrite = File.GetLastWriteTimeUtc(path);

            Assert.Equal(beforeWrite, afterWrite);
        }
        finally { File.Delete(path); }
    }
}
