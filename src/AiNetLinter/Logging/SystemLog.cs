#nullable enable

using System;
using System.Collections.Concurrent;
using System.Reflection;
using Serilog;
using Serilog.Events;

namespace AiNetLinter.Logging;

/// <summary>
/// Prozessweiter Anker des System-Loggings: initialisiert den statischen
/// Serilog-Logger aus der optionalen appsettings.json und stellt die
/// Severity-Klassifizierung fuer gespiegelte Konsolen-Diagnosezeilen bereit.
/// Alle Aufrufe nach <see cref="Initialize"/> sind nicht-throwend - Logging darf
/// den Linter-/Daemon-Betrieb niemals beenden.
/// </summary>
internal static class SystemLog
{
    private static readonly object Gate = new();
    private static readonly ConcurrentDictionary<string, LogEventLevel> LevelMap = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Verbose"] = LogEventLevel.Verbose,
        ["Debug"] = LogEventLevel.Debug,
        ["Information"] = LogEventLevel.Information,
        ["Warning"] = LogEventLevel.Warning,
        ["Error"] = LogEventLevel.Error,
        ["Fatal"] = LogEventLevel.Fatal,
    };

    private static ILogger? bootstrapLogger;

    internal static LoggingConfig Config { get; private set; } = LoggingConfig.CreateDefault();

    internal static bool IsInitialized { get; private set; }

    /// <summary>
    /// Initialisiert den statischen Root-Logger. Genau einmal prozessweit; weitere
    /// Aufrufe sind wirkungslos (Thin-Client und Daemon sind getrennte Prozesse).
    /// </summary>
    internal static void Initialize(string processRole)
    {
        var alreadyInitialized = false;
        lock (Gate)
        {
            if (IsInitialized)
            {
                alreadyInitialized = true;
            }
            else
            {
                IsInitialized = true;
            }
        }

        if (alreadyInitialized) return;

        var config = TryLoadConfig(processRole);
        Config = config;
        ConfigureRoot(config, processRole);

        Log.ForContext("ProcessRole", processRole).Information(
            "System-Logging initialisiert (Level={MinimumLevel}, Verzeichnis={Directory}, BehalteneDateien={RetainedFileCount})",
            config.MinimumLevel,
            config.ResolveDirectory(),
            config.RetainedFileCount);
        Log.ForContext("ProcessRole", processRole).Information(
            "Prozess gestartet: PID={Pid}, Rolle={ProcessRole}, Version={Version}, Args={Args}",
            Environment.ProcessId,
            processRole,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unbekannt",
            Environment.CommandLine);
    }

    /// <summary>Spiegelt eine Diagnosezeile der Konsole ([WARN]/[ERROR]/[FATAL]/[INFO]) ins Log.</summary>
    internal static void WriteConsoleMirror(string message)
    {
        if (!IsInitialized) return;
        var level = Classify(message);
        if (level is null) return;
        Log.Write(level.Value, "CONSOLE {ConsoleLine}", message);
    }

    /// <summary>Wird von Program.cs beim Ende von Main aufgerufen.</summary>
    internal static void CloseAndFlush()
    {
        (Log.Logger as IDisposable)?.Dispose();
    }

    internal static LogEventLevel? Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        if (message.StartsWith("[FATAL ERROR]:", StringComparison.Ordinal)) return LogEventLevel.Fatal;
        if (message.StartsWith("[ERROR]:", StringComparison.Ordinal)) return LogEventLevel.Error;
        if (message.StartsWith("[WARN]:", StringComparison.Ordinal)) return LogEventLevel.Warning;
        if (message.StartsWith("[INFO]:", StringComparison.Ordinal)) return LogEventLevel.Information;
        return null;
    }

    private static LoggingConfig TryLoadConfig(string processRole)
    {
        try
        {
            return LoggingConfigLoader.Load();
        }
        catch (Exception exception)
        {
            bootstrapLogger ??= CreateBootstrapLogger();
            bootstrapLogger.Fatal(exception, "System-Logging: Konfiguration defekt - Abbruch ({ProcessRole})", processRole);
            throw;
        }
    }

    private static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Fatal()
            .WriteTo.File(
                Path.Combine(AppContext.BaseDirectory, "logs", "bootstrap-.log"),
                rollingInterval: RollingInterval.Day,
                shared: true)
            .CreateLogger();
    }

    private static void ConfigureRoot(LoggingConfig config, string processRole)
    {
        var levelSwitch = new Serilog.Core.LoggingLevelSwitch(LevelMap[config.MinimumLevel]);
        var logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.WithProperty("ProcessRole", processRole)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(config.ResolveDirectory(), "ainetlinter-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: config.RetainedFileCount,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ProcessRole}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
        Log.Logger = logger;
    }
}
