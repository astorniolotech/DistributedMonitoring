using DistributedMonitoring.Domain.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
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
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
                .Build();

            _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
            _mqttClient.DisconnectedAsync += OnDisconnectedAsync;

            await _mqttClient.ConnectAsync(options);

            _logService.LogSystem($"Conectado al broker MQTT {config.Broker.Host}:{config.Broker.Port}");

            await SubscribeAsync("NodoPpal");
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error al conectar al broker MQTT: {ex.Message}");
            StartReconnectTimer();
        }
        finally
        {
            lock (_lock)
            {
                _isConnecting = false;
            }
        }
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e)
    {
        _logService.LogCommunication("Desconectado del broker MQTT. Reintentando...");
        StartReconnectTimer();
        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;

        // FIX: usar PayloadSegment en lugar de Payload (Payload esta obsoleto en MQTTnet v4)
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

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
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
    }

    public async Task DisconnectAsync()
    {
        _reconnectTimer?.Dispose();
        _reconnectTimer = null;

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
