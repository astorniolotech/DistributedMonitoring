using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Application.Services;

public class NodeService
{
    private readonly INodeRepository _nodeRepository;
    private readonly IMqttClientService _mqttService;
    private readonly ILogService _logService;
    private readonly SystemConfiguration _systemConfig;
    private readonly Dictionary<int, Timer> _offlineTimers = new();
    private readonly object _lock = new();

    public NodeService(
        INodeRepository nodeRepository,
        IMqttClientService mqttService,
        ILogService logService,
        IConfigurationService configService)
    {
        _nodeRepository = nodeRepository;
        _mqttService = mqttService;
        _logService = logService;
        _systemConfig = configService.GetConfiguration().System;

        // Subscribe to MQTT messages
        _mqttService.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        // Process incoming sensor messages
        // This will be implemented with proper protocol parsing
    }

    public async Task InitializeNodeAsync(int nodeId)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null)
        {
            _logService.LogOperator($"Intento de inicializar nodo inexistente: {nodeId}");
            return;
        }

        node.Status = NodeStatus.Initializing;
        _nodeRepository.UpdateNode(node);
        _logService.LogNode($"Inicializando nodo {nodeId}", nodeId);

        // Send initialization command via MQTT
        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", "INIT_NODO");

        // Start timeout timer
        StartNodeTimeout(nodeId, TimeSpan.FromSeconds(_systemConfig.CommandTimeoutSeconds));
    }

    public async Task RequestValuesAsync(int nodeId)
    {
        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", "GET_VALUES");
    }

    public async Task EnableSensorAsync(int nodeId, int sensorId)
    {
        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", $"ENABLE_SENSOR:{sensorId}");
        _logService.LogOperator($"Habilitar sensor {sensorId} en nodo {nodeId}");
    }

    public async Task DisableSensorAsync(int nodeId, int sensorId)
    {
        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", $"DISABLE_SENSOR:{sensorId}");
        _logService.LogOperator($"Anular sensor {sensorId} en nodo {nodeId}");
    }

    public async Task SetLedAsync(int nodeId, bool on)
    {
        var command = on ? "LED_ON" : "LED_OFF";
        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", command);
    }

    public IEnumerable<Node> GetAllNodes() => _nodeRepository.GetAllNodes();

    public Node? GetNode(int nodeId) => _nodeRepository.GetNode(nodeId);

    private void StartNodeTimeout(int nodeId, TimeSpan timeout)
    {
        lock (_lock)
        {
            if (_offlineTimers.TryGetValue(nodeId, out var existing))
            {
                existing.Dispose();
            }

            var timer = new Timer(_ =>
            {
                HandleNodeTimeout(nodeId);
            }, null, timeout, Timeout.InfiniteTimeSpan);

            _offlineTimers[nodeId] = timer;
        }
    }

    private void HandleNodeTimeout(int nodeId)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node != null && node.Status == NodeStatus.Initializing)
        {
            node.Status = NodeStatus.Unknown;
            _nodeRepository.UpdateNode(node);
            _logService.LogNode($"Timeout inicialización nodo {nodeId}", nodeId);
        }
    }

    public void OnKeepAliveReceived(int nodeId)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null) return;

        var wasOffline = node.Status == NodeStatus.Offline;
        
        node.Status = NodeStatus.Active;
        node.LastSeen = DateTime.Now;
        _nodeRepository.UpdateNode(node);

        // Reset offline timer
        lock (_lock)
        {
            if (_offlineTimers.TryGetValue(nodeId, out var timer))
            {
                timer.Change(
                    TimeSpan.FromMinutes(_systemConfig.OfflineTimeoutMinutes),
                    Timeout.InfiniteTimeSpan);
            }
        }

        if (wasOffline)
        {
            _logService.LogNode($"Nodo {nodeId} reconectado", nodeId);
        }
    }

    public void OnNodeOffline(int nodeId)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null) return;

        node.Status = NodeStatus.Offline;
        _nodeRepository.UpdateNode(node);
        _logService.LogNode($"Nodo {nodeId} fuera de servicio", nodeId);
    }

    public void ProcessSensorData(int nodeId, List<double> values)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null) return;

        node.LastSeen = DateTime.Now;

        // Update sensor values
        for (int i = 0; i < Math.Min(values.Count, node.Sensors.Count); i++)
        {
            node.Sensors[i].RawValue = values[i];
            // Converted value logic would go here
        }

        _nodeRepository.UpdateNode(node);
    }
}