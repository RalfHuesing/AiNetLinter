#nullable enable

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal sealed record ReloadConfigPayload(
    string PreviousConfig,
    string ConfigPath,
    int EnabledRuleCheckCount,
    int EffectiveMetricThresholdCount,
    bool SnapshotChanged);
