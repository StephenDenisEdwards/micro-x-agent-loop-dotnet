using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace MicroXAgentLoop;

public static class LoggingConfig
{
    private const string DefaultLogFile = "agent.log";
    private const long DefaultFileSizeLimit = 10 * 1024 * 1024; // 10 MB
    private const int DefaultRetainedFileCount = 3;

    /// <summary>
    /// Configure Serilog logging sinks. Returns a description of each registered consumer.
    /// </summary>
    public static List<string> SetupLogging(IConfiguration configuration)
    {
        var levelStr = configuration["LogLevel"] ?? "Information";
        var level = ParseLevel(levelStr);

        var consumers = configuration.GetSection("LogConsumers").GetChildren().ToList();
        var descriptions = new List<string>();

        var logConfig = new LoggerConfiguration()
            .MinimumLevel.Is(level);

        if (consumers.Count == 0)
        {
            // Default: console + file
            logConfig.WriteTo.Console(
                restrictedToMinimumLevel: level,
                outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                standardErrorFromLevel: LogEventLevel.Verbose);

            logConfig.WriteTo.File(
                DefaultLogFile,
                restrictedToMinimumLevel: level,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}:{MemberName}:{LineNumber} - {Message:lj}{NewLine}{Exception}",
                fileSizeLimitBytes: DefaultFileSizeLimit,
                retainedFileCountLimit: DefaultRetainedFileCount,
                rollOnFileSizeLimit: true);

            descriptions.Add($"console (stderr, {levelStr})");
            descriptions.Add($"file ({DefaultLogFile}, {levelStr})");
        }
        else
        {
            foreach (var consumer in consumers)
            {
                var sinkType = consumer["type"] ?? "";
                var sinkLevel = consumer["level"] is not null
                    ? ParseLevel(consumer["level"]!)
                    : level;
                var sinkLevelStr = consumer["level"] ?? levelStr;

                switch (sinkType.ToLowerInvariant())
                {
                    case "console":
                        logConfig.WriteTo.Console(
                            restrictedToMinimumLevel: sinkLevel,
                            outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                            standardErrorFromLevel: LogEventLevel.Verbose);
                        descriptions.Add($"console (stderr, {sinkLevelStr})");
                        break;

                    case "file":
                        var path = consumer["path"] ?? DefaultLogFile;
                        var dir = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        logConfig.WriteTo.File(
                            path,
                            restrictedToMinimumLevel: sinkLevel,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}:{MemberName}:{LineNumber} - {Message:lj}{NewLine}{Exception}",
                            fileSizeLimitBytes: DefaultFileSizeLimit,
                            retainedFileCountLimit: DefaultRetainedFileCount,
                            rollOnFileSizeLimit: true);
                        descriptions.Add($"file ({path}, {sinkLevelStr})");
                        break;

                    default:
                        Console.Error.WriteLine($"Warning: Unknown log consumer type: {sinkType}");
                        break;
                }
            }
        }

        Log.Logger = logConfig.CreateLogger();
        return descriptions;
    }

    private static LogEventLevel ParseLevel(string level) => level.ToLowerInvariant() switch
    {
        "verbose" or "trace" => LogEventLevel.Verbose,
        "debug" => LogEventLevel.Debug,
        "information" or "info" => LogEventLevel.Information,
        "warning" or "warn" => LogEventLevel.Warning,
        "error" => LogEventLevel.Error,
        "fatal" or "critical" => LogEventLevel.Fatal,
        _ => LogEventLevel.Information,
    };
}
