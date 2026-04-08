using DistributedMonitoring.Domain.Interfaces;
using Serilog;
using Serilog.Core;

namespace DistributedMonitoring.Infrastructure.Logging;

public class LogService : ILogService
{
    private readonly string _logPath;
    private readonly Logger _logger;
    private readonly List<LogEvent> _recentLogs = new();
    private readonly object _lock = new();
    private const int MaxRecentLogs = 1000;

    public LogService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "DistributedMonitoring");
        
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        _logPath = Path.Combine(appFolder, "novedades.txt");

        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(
                _logPath,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} | {Level:u3} | {Message:lj}{NewLine}",
                rollingInterval: RollingInterval.Infinite,
                retainedFileTimeLimit: TimeSpan.Zero) // No rotation - single continuous file
            .CreateLogger();

        _logger.Information("Sistema iniciado");
    }

    public void LogSystem(string message) => Log(LogLevel.Info, LogCategory.System, message, null);
    public void LogAlarm(string message, int? nodeId = null) => Log(LogLevel.Warning, LogCategory.Alarm, message, nodeId);
    public void LogNode(string message, int nodeId) => Log(LogLevel.Info, LogCategory.Node, message, nodeId);
    public void LogOperator(string message) => Log(LogLevel.Info, LogCategory.Operator, message, null);
    public void LogCommunication(string message) => Log(LogLevel.Warning, LogCategory.Communication, message, null);

    private void Log(LogLevel level, LogCategory category, string message, int? nodeId)
    {
        var logEvent = new LogEvent
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category,
            Message = message,
            NodeId = nodeId
        };

        // Add to recent logs
        lock (_lock)
        {
            _recentLogs.Add(logEvent);
            if (_recentLogs.Count > MaxRecentLogs)
            {
                _recentLogs.RemoveAt(0);
            }
        }

        // Log to Serilog
        _logger.Information("{Category} | {Message}", category, message);
    }

    public IEnumerable<LogEvent> GetRecentLogs(int count)
    {
        lock (_lock)
        {
            return _recentLogs.TakeLast(count).ToList();
        }
    }
}