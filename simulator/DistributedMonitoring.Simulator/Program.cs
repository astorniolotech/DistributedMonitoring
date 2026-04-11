using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Text;

namespace DistributedMonitoring.Simulator;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Simulador de Nodos de Sensores ===");
        
        // Configuración
        string brokerHost = args.Length > 0 ? args[0] : "192.168.1.100";
        int brokerPort = args.Length > 1 ? int.Parse(args[1]) : 1883;
        int nodeCount = args.Length > 2 ? int.Parse(args[2]) : 2;
        
        Console.WriteLine($"Broker: {brokerHost}:{brokerPort}");
        Console.WriteLine($"Nodos a simular: {nodeCount}");
        Console.WriteLine();

        var simulator = new NodeSimulator(brokerHost, brokerPort, nodeCount);
        
        Console.WriteLine("Presiona Enter para iniciar...");
        Console.ReadLine();
        
        await simulator.StartAsync();
        
        Console.WriteLine("Simulador iniciado. Presiona Enter para detener...");
        Console.ReadLine();
        
        await simulator.StopAsync();
    }
}

class NodeSimulator
{
    private readonly string _brokerHost;
    private readonly int _brokerPort;
    private readonly int _nodeCount;
    private IMqttClient? _mqttClient;
    private readonly List<SimulatedNode> _nodes = new();
    private Timer? _keepAliveTimer;
    private Timer? _dataTimer;
    private bool _isRunning;

    public NodeSimulator(string brokerHost, int brokerPort, int nodeCount)
    {
        _brokerHost = brokerHost;
        _brokerPort = brokerPort;
        _nodeCount = Math.Min(nodeCount, 5); // Max 5 nodos
    }

    public async Task StartAsync()
    {
        // Create MQTT client
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerHost, _brokerPort)
            .WithClientId("SimuladorNodos")
            .WithCleanSession()
            .Build();

        try
        {
            await _mqttClient.ConnectAsync(options);
            Console.WriteLine($"✓ Conectado al broker MQTT");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error al conectar: {ex.Message}");
            return;
        }

        // Subscribe to commands
        await _mqttClient.SubscribeAsync("NodoSensor/#");
        _mqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

        // Initialize simulated nodes
        for (int i = 1; i <= _nodeCount; i++)
        {
            _nodes.Add(new SimulatedNode
            {
                Id = i,
                Name = $"Sensor {('A' + i - 1)}",
                Status = "ACTIVE",
                LastSeen = DateTime.Now
            });
        }

        _isRunning = true;

        // Start KeepAlive timer (every 60 seconds)
        _keepAliveTimer = new Timer(SendKeepAlive, null, TimeSpan.Zero, TimeSpan.FromSeconds(60));

        // Start data publishing (every 5 seconds)
        _dataTimer = new Timer(SendSensorData, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));

        Console.WriteLine("✓ Nodos simulados iniciados");
        Console.WriteLine("  - KeepAlive: cada 60s");
        Console.WriteLine("  - Datos: cada 5s");
    }

    public async Task StopAsync()
    {
        _isRunning = false;
        _keepAliveTimer?.Dispose();
        _dataTimer?.Dispose();

        if (_mqttClient?.IsConnected == true)
        {
            await _mqttClient.DisconnectAsync();
        }

        Console.WriteLine("✓ Simulador detenido");
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        
        Console.WriteLine($"  → Comando recibido: {topic} - {payload}");

        // Parse node ID from topic (NodoSensor/0 = broadcast, NodoSensor/{id} = specific)
        var parts = topic.Split('/');
        if (parts.Length < 2) return Task.CompletedTask;
        
        int? targetNodeId = parts[1] == "0" ? null : int.Parse(parts[1]);

        // Process command
        string response = ProcessCommand(payload, targetNodeId);
        
        if (!string.IsNullOrEmpty(response))
        {
            // Send response to NodoPpal
            PublishMessage(response);
        }

        return Task.CompletedTask;
    }

    private string ProcessCommand(string command, int? targetNodeId)
    {
        // Filter by target node if specified
        var targetNodes = targetNodeId.HasValue 
            ? _nodes.Where(n => n.Id == targetNodeId.Value) 
            : _nodes.AsEnumerable();

        foreach (var node in targetNodes)
        {
            switch (command)
            {
                case "INIT_NODO":
                    node.Status = "INITIALIZING";
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
                
                case "INIT_SENSORES":
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
                
                case var c when c.StartsWith("ENABLE_SENSOR:"):
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
                
                case var c when c.StartsWith("DISABLE_SENSOR:"):
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
                
                case "GET_VALUES":
                    return GenerateSensorData(node.Id);
                
                case "GET_CONFIG":
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "CONFIG");
                
                case "LED_ON":
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
                
                case "LED_OFF":
                    return FormatMessage(node.Id, 3, new[] { 0.0, 0.0, 0.0, 0.0 }, "_RXOK");
            }
        }

        return string.Empty;
    }

    private void SendKeepAlive(object? state)
    {
        if (!_isRunning) return;

        foreach (var node in _nodes)
        {
            var message = FormatMessage(node.Id, 5, new[] { 0.0, 0.0, 0.0, 0.0 }, "_ESTASVIVO");
            PublishMessage(message);
        }

        Console.WriteLine("  ✓ KeepAlive enviado");
    }

    private void SendSensorData(object? state)
    {
        if (!_isRunning) return;

        foreach (var node in _nodes)
        {
            var data = GenerateSensorData(node.Id);
            PublishMessage(data);
        }

        Console.WriteLine("  ✓ Datos de sensores enviados");
    }

    private string GenerateSensorData(int nodeId)
    {
        // Generate realistic sensor values with some variation
        var random = new Random();
        var values = new double[4];
        
        // Temperature: 20-80°C
        values[0] = 40 + random.NextDouble() * 30;
        
        // Pressure: 1-8 bar
        values[1] = 3 + random.NextDouble() * 4;
        
        // Flow: 10-90 L/min
        values[2] = 20 + random.NextDouble() * 60;
        
        // Level: 10-90%
        values[3] = 30 + random.NextDouble() * 50;

        return FormatMessage(nodeId, 1, values, "OK");
    }

    private string FormatMessage(int nodeId, int messageType, double[] values, string status)
    {
        // Format: $TYPE,NODEID,S1,S2,S3,S4,STATUS,FLAGS,CHECKSUM#
        var s1 = values.Length > 0 ? values[0].ToString("F1") : "0";
        var s2 = values.Length > 1 ? values[1].ToString("F1") : "0";
        var s3 = values.Length > 2 ? values[2].ToString("F1") : "0";
        var s4 = values.Length > 3 ? values[3].ToString("F1") : "0";

        var payload = $"{messageType},{nodeId},{s1},{s2},{s3},{s4},{status},0";
        var checksum = CalculateChecksum(payload);

        return $"${payload}*{checksum}#";
    }

    private string CalculateChecksum(string payload)
    {
        int sum = payload.Sum(c => (byte)c);
        return (sum & 0xFF).ToString("X2");
    }

    private void PublishMessage(string message)
    {
        if (_mqttClient?.IsConnected != true) return;

        var mqttMessage = new MqttApplicationMessageBuilder()
            .WithTopic("NodoPpal")
            .WithPayload(Encoding.UTF8.GetBytes(message))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        _ = _mqttClient.PublishAsync(mqttMessage);
    }
}

class SimulatedNode
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
}