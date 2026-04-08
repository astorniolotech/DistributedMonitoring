using DistributedMonitoring.Domain.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using System.Text;

namespace DistributedMonitoring.Infrastructure.MQTT;

public class MqttClientService : IMqttClientService, IDisposable
{
    private IMqttClient? _mqttClient;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logService;
    private Timer? _reconnectTimer;
    private bool _isConnecting;
    private readonly object _lock = new();

    public bool IsConnected => _mqttClient?.IsConnected ?? false;
    public event EventHandler<MqttMessageReceivedEventArgs>? MessageReceived;

    public MqttClientService(IConfigurationService configService, ILogService logService)
    {
        _configService = configService;
        _logService = logService;
    }

    public async Task ConnectAsync()
    {
        if (_isConnecting) return;
        
        lock (_lock)
        {
            if (_isConnecting) return;
            _isConnecting = true;
        }

        try
        {
            var config = _configService.GetConfiguration();
            
            var factory = new MqttFactory();
            _mqttClient = factory.CreateMqttClient();

            var options = new MqttClientOptionsBuilder()
                .WithTcpServer(config.Broker.Host, config.Broker.Port)
                .WithClientId(config.Broker.ClientId)
                .WithCleanSession()
                .WithKeepAlive(TimeSpan.FromSeconds(30))
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

            await _mqttClient.ConnectAsync(options);
            
            _logService.LogSystem($"Conectado al broker MQTT {config.Broker.Host}:{config.Broker.Port}");
            
            // Subscribe to topics
            await SubscribeAsync("NodoPpal");
            
            lock (_lock)
            {
                _isConnecting = false;
            }
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error al conectar al broker MQTT: {ex.Message}");
            
            lock (_lock)
            {
                _isConnecting = false;
            }
            
            // Start reconnection timer
            StartReconnectTimer();
        }
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        
        MessageReceived?.Invoke(this, new MqttMessageReceivedEventArgs
        {
            Topic = topic,
            Message = payload
        });

        return Task.CompletedTask;
    }

    private void StartReconnectTimer()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = new Timer(async _ =>
        {
            if (!IsConnected && !_isConnecting)
            {
                await ConnectAsync();
            }
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public async Task DisconnectAsync()
    {
        _reconnectTimer?.Dispose();
        
        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync();
            _logService.LogSystem("Desconectado del broker MQTT");
        }
    }

    public async Task PublishAsync(string topic, string message)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logService.LogCommunication($"No conectado - no se puede publicar en {topic}");
            return;
        }

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(Encoding.UTF8.GetBytes(message))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(mqttMessage);
    }

    public async Task SubscribeAsync(string topic)
    {
        if (_mqttClient?.IsConnected != true)
        {
            _logService.LogCommunication($"No conectado - no se puede suscribir a {topic}");
            return;
        }

        var options = new MqttTopicFilterBuilder()
            .WithTopic(topic)
            .Build();

        await _mqttClient.SubscribeAsync(options);
    }

    public void Dispose()
    {
        _reconnectTimer?.Dispose();
        _mqttClient?.Dispose();
    }
}

// Simple in-memory implementation of INodeRepository
public class NodeRepository : INodeRepository
{
    private readonly Dictionary<int, Node> _nodes = new();
    private readonly IConfigurationService _configService;
    private readonly object _lock = new();

    public NodeRepository(IConfigurationService configService)
    {
        _configService = configService;
        InitializeNodes();
    }

    private void InitializeNodes()
    {
        var config = _configService.GetConfiguration();
        
        foreach (var nodeConfig in config.Nodes)
        {
            var node = new Node
            {
                Id = nodeConfig.Id,
                Name = nodeConfig.Name,
                IsEnabled = nodeConfig.Enabled,
                Status = NodeStatus.Unknown,
                Sensors = nodeConfig.Sensors.Select(s => new Sensor
                {
                    Id = s.Id,
                    Name = s.Name,
                    Unit = s.Unit,
                    IsEnabled = true,
                    Limits = s.Limits
                }).ToList()
            };
            
            _nodes[node.Id] = node;
        }
    }

    public Node? GetNode(int id)
    {
        lock (_lock)
        {
            return _nodes.TryGetValue(id, out var node) ? node : null;
        }
    }

    public IEnumerable<Node> GetAllNodes()
    {
        lock (_lock)
        {
            return _nodes.Values.ToList();
        }
    }

    public void UpdateNode(Node node)
    {
        lock (_lock)
        {
            _nodes[node.Id] = node;
        }
    }
}

// Simple in-memory message bus
public class MessageBus : IMessageBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly object _lock = new();

    public void Subscribe<T>(Action<T> handler)
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
            {
                _handlers[type] = new List<Delegate>();
            }
            _handlers[type].Add(handler);
        }
    }

    public void Publish<T>(T message)
    {
        lock (_lock)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    ((Action<T>)handler)(message);
                }
            }
        }
    }
}