#nullable enable

using System;
using System.Collections.Generic;

namespace AiNetLinter.Mcp.Daemon;

/// <summary>
/// Abstraktion für den persistenten MRU-Projektzustand des Daemons.
/// </summary>
internal interface IMruStateStore : IAsyncDisposable
{
    IReadOnlyList<MruStateEntry> Read(int maxProjects);
    void Touch(string rootPath, DateTime? lastUsedUtc = null);
    void Remove(string rootPath);
}
