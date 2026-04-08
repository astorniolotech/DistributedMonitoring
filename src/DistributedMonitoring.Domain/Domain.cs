// Domain Layer - Interfaces
namespace DistributedMonitoring.Domain.Interfaces;

public interface IConfigurationService
{
    AppConfiguration GetConfiguration();
    void SaveConfiguration(AppConfiguration config);
}

public interface IMessageBus
{
    void Subscribe<T>(Action<T> handler);
    void Publish<T>(T message);
}

public interface INodeRepository
{
    Node? GetNode(int id);
    IEnumerable<Node> GetAllNodes();
    void UpdateNode(Node node);
}

public interface ILogService
{
    void LogSystem(string message);
    void LogAlarm(string message, int? nodeId = null);
    void LogNode(string message, int nodeId);
    void LogOperator(string message);
    void LogCommunication(string message);
    IEnumerable<LogEvent> GetRecentLogs(int count);
}

public interface IMqttClientService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    Task PublishAsync(string topic, string message);
    Task SubscribeAsync(string topic);
    bool IsConnected { get; }
    event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;
}

public interface ISerialPortService
{
    Task OpenAsync(string portName);
    Task CloseAsync();
    Task SendAsync(string message);
    bool IsOpen { get; }
    event EventHandler<string>? DataReceived;
}

// DTOs for configuration
public class AppConfiguration
{
    public BrokerConfiguration Broker { get; set; } = new();
    public List<NodeConfiguration> Nodes { get; set; } = new();
    public AlarmConfiguration Alarm { get; set; } = new();
    public SystemConfiguration System { get; set; } = new();
}

public class BrokerConfiguration
{
    public string Host { get; set; } = "192.168.1.100";
    public int Port { get; set; } = 1883;
    public string ClientId { get; set; } = "NodoPrincipal";
}

public class NodeConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<SensorConfiguration> Sensors { get; set; } = new();
}

public class SensorConfiguration
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public SensorLimits Limits { get; set; } = new();
}

public class SensorLimits
{
    public double RawLowAlarm { get; set; }
    public double RawHighAlarm { get; set; }
    public double RawLowWarning { get; set; }
    public double RawHighWarning { get; set; }
}

public class AlarmConfiguration
{
    public int SirenNodeId { get; set; } = 999;
    public bool AutoActivate { get; set; } = true;
}

public class SystemConfiguration
{
    public int OfflineTimeoutMinutes { get; set; } = 5;
    public int KeepAliveIntervalSeconds { get; set; } = 60;
    public int CommandTimeoutSeconds { get; set; } = 10;
}

// Event args
public class MqttMessageReceivedEventArgs : EventArgs
{
    public string Topic { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// Domain entities
public class Node
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public NodeStatus Status { get; set; } = NodeStatus.Unknown;
    public bool IsEnabled { get; set; } = true;
    public DateTime LastSeen { get; set; }
    public List<Sensor> Sensors { get; set; } = new();
}

public enum NodeStatus
{
    Unknown,
    Initializing,
    Active,
    Offline
}

public class Sensor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public double? RawValue { get; set; }
    public double? ConvertedValue { get; set; }
    public SensorState State { get; set; } = SensorState.OK;
    public SensorLimits Limits { get; set; } = new();
}

public enum SensorState
{
    OK,
    LowAlarm,
    HighAlarm,
    LowWarning,
    HighWarning,
    Disabled
}

public class Alarm
{
    public int Id { get; set; }
    public AlarmType Type { get; set; }
    public AlarmLevel Level { get; set; }
    public int? NodeId { get; set; }
    public int? SensorId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public bool IsAcknowledged { get; set; }
    public bool IsSilenced { get; set; }
}

public enum AlarmType
{
    SensorLowAlarm,
    SensorHighAlarm,
    SensorLowWarning,
    SensorHighWarning,
    NodeOffline,
    CommunicationError
}

public enum AlarmLevel
{
    Warning,
    Alarm
}

public class SensorMessage
{
    public string RawContent { get; set; } = string.Empty;
    public int NodeId { get; set; }
    public MessageType Type { get; set; }
    public List<double> Values { get; set; } = new();
    public bool IsValid { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public enum MessageType
{
    Data,
    Status,
    Response,
    Error,
    KeepAlive
}

public class LogEvent
{
    public DateTime Timestamp { get; set; }
    public LogLevel Level { get; set; }
    public LogCategory Category { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? NodeId { get; set; }
}

public enum LogLevel
{
    Info,
    Warning,
    Error
}

public enum LogCategory
{
    Alarm,
    Node,
    Operator,
    Communication,
    System
}