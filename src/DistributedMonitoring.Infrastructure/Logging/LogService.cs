using DistributedMonitoring.Domain.Interfaces;
using Serilog;
// FIX: alias explicito para evitar conflicto entre Domain.Interfaces.LogEvent y Serilog.Events.LogEvent
using DomainLogEvent = DistributedMonitoring.Domain.Interfaces.LogEvent;
using DomainLogLevel = DistributedMonitoring.Domain.Interfaces.LogLevel;

namespace DistributedMonitoring.Infrastructure.Logging;

public class LogService : ILogService
{
    private readonly string _logPath;
    private readonly Serilog.Core.Logger _logger;
    private readonly List<DomainLogEvent> _recentLogs = new();
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
                retainedFileTimeLimit: null)
            .CreateLogger();

        _logger.Information("Sistema iniciado");
    }

    public void LogSystem(string message) => Log(DomainLogLevel.Info, LogCategory.System, message, null);
    public void LogAlarm(string message, int? nodeId = null) => Log(DomainLogLevel.Warning, LogCategory.Alarm, message, nodeId);
    public void LogNode(string message, int nodeId) => Log(DomainLogLevel.Info, LogCategory.Node, message, nodeId);
    public void LogOperator(string message) => Log(DomainLogLevel.Info, LogCategory.Operator, message, null);
    public void LogCommunication(string message) => Log(DomainLogLevel.Warning, LogCategory.Communication, message, null);

    private void Log(DomainLogLevel level, LogCategory category, string message, int? nodeId)
    {
        var logEvent = new DomainLogEvent
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category,
            Message = message,
            NodeId = nodeId
        };

        lock (_lock)
        {
            _recentLogs.Add(logEvent);
            if (_recentLogs.Count > MaxRecentLogs)
                _recentLogs.RemoveAt(0);
        }

        // Mapear nivel del dominio a nivel de Serilog
        if (level == DomainLogLevel.Warning || level == DomainLogLevel.Error)
            _logger.Warning("[{Category}] {Message}", category, message);
        else
            _logger.Information("[{Category}] {Message}", category, message);
    }

    public IEnumerable<DomainLogEvent> GetRecentLogs(int count)
    {
        lock (_lock)
        {
            return _recentLogs.TakeLast(count).ToList();
        }
    }
}
