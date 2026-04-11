using DistributedMonitoring.Domain.Interfaces;

namespace DistributedMonitoring.Application.Services;

public class AlarmService
{
    private readonly ILogService _logService;
    private readonly IMqttClientService _mqttService;
    private readonly IConfigurationService _configService;
    private readonly List<Alarm> _activeAlarms = new();
    private readonly object _lock = new();
    private bool _sirenActive;
    private bool _sirenSilenced;

    public event EventHandler<Alarm>? AlarmTriggered;
    public event EventHandler? AlarmSilenced;

    public AlarmService(
        ILogService logService,
        IMqttClientService mqttService,
        IConfigurationService configService)
    {
        _logService = logService;
        _mqttService = mqttService;
        _configService = configService;
    }

    public void EvaluateSensor(int nodeId, int sensorId, double value, Sensor sensor)
    {
        if (!sensor.IsEnabled) return;

        var limits = sensor.Limits;
        SensorState newState;

        if (value < limits.RawLowAlarm)
            newState = SensorState.LowAlarm;
        else if (value > limits.RawHighAlarm)
            newState = SensorState.HighAlarm;
        else if (value < limits.RawLowWarning)
            newState = SensorState.LowWarning;
        else if (value > limits.RawHighWarning)
            newState = SensorState.HighWarning;
        else
            newState = SensorState.OK;

        sensor.State = newState;

        // Only trigger alarm on state change
        if (newState != SensorState.OK && newState != sensor.State)
        {
            var alarmType = newState switch
            {
                SensorState.LowAlarm => AlarmType.SensorLowAlarm,
                SensorState.HighAlarm => AlarmType.SensorHighAlarm,
                SensorState.LowWarning => AlarmType.SensorLowWarning,
                SensorState.HighWarning => AlarmType.SensorHighWarning,
                _ => AlarmType.CommunicationError
            };

            var level = newState switch
            {
                SensorState.LowAlarm or SensorState.HighAlarm => AlarmLevel.Alarm,
                _ => AlarmLevel.Warning
            };

            TriggerAlarm(alarmType, level, nodeId, sensorId, $"Sensor {sensor.Name}: {newState}");
        }
    }

    private void TriggerAlarm(AlarmType type, AlarmLevel level, int? nodeId, int? sensorId, string description)
    {
        lock (_lock)
        {
            var alarm = new Alarm
            {
                Id = (int)DateTime.Now.Ticks,
                Type = type,
                Level = level,
                NodeId = nodeId,
                SensorId = sensorId,
                Description = description,
                TriggeredAt = DateTime.Now,
                IsAcknowledged = false,
                IsSilenced = _sirenSilenced
            };

            _activeAlarms.Add(alarm);
            _logService.LogAlarm($"Alarma: {description}", nodeId);

            // Activate siren for Alarm level
            if (level == AlarmLevel.Alarm && !_sirenSilenced)
            {
                ActivateSiren();
            }

            AlarmTriggered?.Invoke(this, alarm);
        }
    }

    private async void ActivateSiren()
    {
        if (_sirenActive) return;
        
        _sirenActive = true;
        var sirenNodeId = _configService.GetConfiguration().Alarm.SirenNodeId;
        
        try
        {
            await _mqttService.PublishAsync($"NodoSensor/{sirenNodeId}", "ALARM_ON");
        }
        catch
        {
            // Log but don't crash
        }
    }

    public async Task SilencesSirenAsync()
    {
        _sirenSilenced = true;
        
        foreach (var alarm in _activeAlarms)
        {
            alarm.IsSilenced = true;
        }

        var sirenNodeId = _configService.GetConfiguration().Alarm.SirenNodeId;
        
        try
        {
            await _mqttService.PublishAsync($"NodoSensor/{sirenNodeId}", "ALARM_OFF");
        }
        catch
        {
            // Log but don't crash
        }

        _logService.LogAlarm("Alarma silenciada por operador");
        AlarmSilenced?.Invoke(this, EventArgs.Empty);
    }

    public IEnumerable<Alarm> GetActiveAlarms()
    {
        lock (_lock)
        {
            return _activeAlarms.Where(a => !a.IsAcknowledged).ToList();
        }
    }

    public void AcknowledgeAlarm(int alarmId)
    {
        lock (_lock)
        {
            var alarm = _activeAlarms.FirstOrDefault(a => a.Id == alarmId);
            if (alarm != null)
            {
                alarm.IsAcknowledged = true;
            }
        }
    }

    public void TriggerNodeOfflineAlarm(int nodeId)
    {
        TriggerAlarm(AlarmType.NodeOffline, AlarmLevel.Warning, nodeId, null, $"Nodo {nodeId} fuera de servicio");
    }
}