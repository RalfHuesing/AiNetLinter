#nullable enable

namespace AiNetLinter.Mcp.Tools.ServerMaintenance;

internal sealed record ReloadConfigPayload(
    string PreviousConfig,
    string ConfigPath,
    int PreviousEnabledRuleCount,
    int EnabledRuleCount,
    int EnabledRuleDelta);
