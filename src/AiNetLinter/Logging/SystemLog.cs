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

    private const string LogFileName = "ainetlinter-.log";

    private static LoggingConfig config = LoggingConfig.CreateDefault();
    private static bool initialized;

    internal static LoggingConfig Config
    {
        get
        {
            lock (Gate) return config;
        }
    }

    internal static bool IsInitialized
    {
        get
        {
            lock (Gate) return initialized;
        }
    }

    /// <summary>
    /// Initialisiert den statischen Root-Logger. Genau einmal prozessweit; weitere
    /// Aufrufe sind wirkungslos (Thin-Client und Daemon sind getrennte Prozesse).
    /// </summary>
    internal static void Initialize(string processRole)
    {
        lock (Gate)
        {
            if (initialized) return;

            LoggingConfig loadedConfig;
            try
            {
                loadedConfig = LoggingConfigLoader.Load();
            }
            catch (Exception exception)
            {
                var fallbackConfig = LoggingConfig.CreateDefault();
                config = fallbackConfig;
                var fallbackLogger = CreateRootLogger(fallbackConfig, processRole);
                try
                {
                    fallbackLogger.Fatal(exception, "System-Logging: Konfiguration defekt - Abbruch ({ProcessRole})", processRole);
                }
                finally
                {
                    (fallbackLogger as IDisposable)?.Dispose();
                }

                throw;
            }

            var logger = CreateRootLogger(loadedConfig, processRole);
            config = loadedConfig;
            Log.Logger = logger;

            logger.ForContext("ProcessRole", processRole).Information(
                "System-Logging initialisiert (Level={MinimumLevel}, Verzeichnis={Directory}, BehalteneDateien={RetainedFileCount})",
                loadedConfig.MinimumLevel,
                loadedConfig.ResolveDirectory(),
                loadedConfig.RetainedFileCount);
            WriteProcessStartLog(logger, processRole);
            initialized = true;
        }
    }

    internal static void WriteProcessStartLog(ILogger logger, string processRole)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var diagnostics = GetProcessStartDiagnostics();
        logger.ForContext("ProcessRole", processRole).Information(
            "Prozess gestartet: PID={Pid}, Rolle={ProcessRole}, Version={Version}, Executable={Executable}, ArgumentCount={ArgumentCount}",
            diagnostics.ProcessId,
            processRole,
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unbekannt",
            diagnostics.ProcessPath,
            diagnostics.ArgumentCount);
    }

    internal static (int ProcessId, string ProcessPath, int ArgumentCount) GetProcessStartDiagnostics() =>
        new(
            Environment.ProcessId,
            Environment.ProcessPath ?? "unbekannt",
            Math.Max(0, Environment.GetCommandLineArgs().Length - 1));

    /// <summary>Spiegelt eine Diagnosezeile der Konsole ([WARN]/[ERROR]/[FATAL]/[INFO]) ins Log.</summary>
    internal static void WriteConsoleMirror(string message)
    {
        if (!IsInitialized) return;
        var level = Classify(message);
        if (level is null) return;
        Log.Write(level.Value, "CONSOLE {ConsoleLine}", message);
    }

    /// <summary>Wird von Program.cs beim Ende von Main aufgerufen.</summary>
    internal static void CloseAndFlush() => Log.CloseAndFlush();

    internal static LogEventLevel? Classify(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        if (message.StartsWith("[FATAL ERROR]:", StringComparison.Ordinal)) return LogEventLevel.Fatal;
        if (message.StartsWith("[ERROR]:", StringComparison.Ordinal)) return LogEventLevel.Error;
        if (message.StartsWith("[WARN]:", StringComparison.Ordinal)) return LogEventLevel.Warning;
        if (message.StartsWith("[INFO]:", StringComparison.Ordinal)) return LogEventLevel.Information;
        return null;
    }

    private static ILogger CreateRootLogger(LoggingConfig config, string processRole)
    {
        var levelSwitch = new Serilog.Core.LoggingLevelSwitch(LevelMap[config.MinimumLevel]);
        return new LoggerConfiguration()
            .MinimumLevel.ControlledBy(levelSwitch)
            .Enrich.WithProperty("ProcessRole", processRole)
            .Enrich.WithProperty("Pid", Environment.ProcessId)
            .WriteTo.File(
                Path.Combine(config.ResolveDirectory(), LogFileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: config.RetainedFileCount,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ProcessRole}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

}
