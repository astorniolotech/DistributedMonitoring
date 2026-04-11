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
    private int _nextAlarmId = 1; // FIX: usar contador en lugar de DateTime.Ticks (evita overflow long->int)

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

        // FIX: guardar estado anterior ANTES de asignar el nuevo
        // Bug original: sensor.State = newState se ejecutaba antes de la comparacion,
        // por lo que newState != sensor.State siempre era false y nunca se disparaba la alarma
        var previousState = sensor.State;

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

        // Solo disparar alarma si el estado cambio y no es OK
        if (newState != SensorState.OK && newState != previousState)
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
                Id = _nextAlarmId++, // FIX: contador incremental thread-safe dentro del lock
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

            // Activar sirena solo para nivel Alarm y si no esta silenciada
            if (level == AlarmLevel.Alarm && !_sirenSilenced)
            {
                _ = ActivateSirenAsync(); // FIX: async Task en lugar de async void
            }

            AlarmTriggered?.Invoke(this, alarm);
        }
    }

    // FIX: convertido de async void a async Task para que las excepciones no sean silenciadas
    private async Task ActivateSirenAsync()
    {
        if (_sirenActive) return;

        _sirenActive = true;
        var sirenNodeId = _configService.GetConfiguration().Alarm.SirenNodeId;

        try
        {
            await _mqttService.PublishAsync($"NodoSensor/{sirenNodeId}", "ALARM_ON");
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error activando sirena: {ex.Message}");
        }
    }

    public async Task SilenceSirenAsync()
    {
        lock (_lock)
        {
            _sirenSilenced = true;
            _sirenActive = false;

            foreach (var alarm in _activeAlarms)
            {
                alarm.IsSilenced = true;
            }
        }

        var sirenNodeId = _configService.GetConfiguration().Alarm.SirenNodeId;

        try
        {
            await _mqttService.PublishAsync($"NodoSensor/{sirenNodeId}", "ALARM_OFF");
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error silenciando sirena: {ex.Message}");
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
