using DistributedMonitoring.Domain.Interfaces;
using DistributedMonitoring.Infrastructure.Protocol;

namespace DistributedMonitoring.Application.Services;

public class NodeService
{
    private readonly INodeRepository _nodeRepository;
    private readonly IMqttClientService _mqttService;
    private readonly ILogService _logService;
    private readonly AlarmService _alarmService;
    private readonly SystemConfiguration _systemConfig;
    private readonly Dictionary<int, Timer> _offlineTimers = new();
    private readonly object _lock = new();
    private readonly ProtocolParser _protocolParser = new();

    public NodeService(
        INodeRepository nodeRepository,
        IMqttClientService mqttService,
        ILogService logService,
        IConfigurationService configService,
        AlarmService alarmService)
    {
        _nodeRepository = nodeRepository;
        _mqttService = mqttService;
        _logService = logService;
        _alarmService = alarmService;
        _systemConfig = configService.GetConfiguration().System;

        // Suscribirse a mensajes MQTT
        _mqttService.MessageReceived += OnMessageReceived;
    }

    // FIX: implementado el procesamiento real de mensajes MQTT usando ProtocolParser
    private void OnMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        // Solo procesar mensajes del topic del nodo principal
        if (!e.Topic.Equals("NodoPpal", StringComparison.OrdinalIgnoreCase))
            return;

        var message = _protocolParser.Parse(e.Message);
        if (message == null || !message.IsValid)
        {
            _logService.LogCommunication($"Mensaje invalido recibido: {e.Message}");
            return;
        }

        switch (message.Type)
        {
            case MessageType.Data:
                ProcessSensorData(message.NodeId, message.Values);
                OnKeepAliveReceived(message.NodeId); // dato recibido = nodo activo
                break;

            case MessageType.KeepAlive:
                OnKeepAliveReceived(message.NodeId);
                _logService.LogCommunication($"KeepAlive recibido de nodo {message.NodeId}");
                break;

            case MessageType.Response:
                HandleNodeResponse(message.NodeId);
                break;

            case MessageType.Status:
                _logService.LogNode($"Status de nodo {message.NodeId}", message.NodeId);
                OnKeepAliveReceived(message.NodeId);
                break;

            case MessageType.Error:
                _logService.LogNode($"Error reportado por nodo {message.NodeId}", message.NodeId);
                _alarmService.TriggerNodeOfflineAlarm(message.NodeId);
                break;
        }
    }

    private void HandleNodeResponse(int nodeId)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null) return;

        if (node.Status == NodeStatus.Initializing)
        {
            node.Status = NodeStatus.Active;
            node.LastSeen = DateTime.Now;
            _nodeRepository.UpdateNode(node);
            _logService.LogNode($"Nodo {nodeId} inicializado correctamente", nodeId);

            // Cancelar timer de timeout
            lock (_lock)
            {
                if (_offlineTimers.TryGetValue(nodeId, out var timer))
                {
                    timer.Dispose();
                    _offlineTimers.Remove(nodeId);
                }
            }
        }
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

        await _mqttService.PublishAsync($"NodoSensor/{nodeId}", "INIT_NODO");

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
            _logService.LogNode($"Timeout inicializacion nodo {nodeId}", nodeId);
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

        // Reiniciar timer de offline
        lock (_lock)
        {
            if (_offlineTimers.TryGetValue(nodeId, out var timer))
            {
                timer.Change(
                    TimeSpan.FromMinutes(_systemConfig.OfflineTimeoutMinutes),
                    Timeout.InfiniteTimeSpan);
            }
            else
            {
                // Crear timer si no existe
                var newTimer = new Timer(_ => OnNodeOffline(nodeId), null,
                    TimeSpan.FromMinutes(_systemConfig.OfflineTimeoutMinutes),
                    Timeout.InfiniteTimeSpan);
                _offlineTimers[nodeId] = newTimer;
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
        _alarmService.TriggerNodeOfflineAlarm(nodeId);
    }

    public void ProcessSensorData(int nodeId, List<double> values)
    {
        var node = _nodeRepository.GetNode(nodeId);
        if (node == null) return;

        node.LastSeen = DateTime.Now;

        for (int i = 0; i < Math.Min(values.Count, node.Sensors.Count); i++)
        {
            node.Sensors[i].RawValue = values[i];

            // Evaluar alarmas para cada sensor
            _alarmService.EvaluateSensor(nodeId, node.Sensors[i].Id, values[i], node.Sensors[i]);
        }

        _nodeRepository.UpdateNode(node);
    }
}
