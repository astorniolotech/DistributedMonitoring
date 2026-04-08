using DistributedMonitoring.Domain.Interfaces;
using System.Text.Json;

namespace DistributedMonitoring.Infrastructure.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly string _configPath;
    private AppConfiguration _config;

    public ConfigurationService()
    {
        // Use AppData folder in production
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appData, "DistributedMonitoring");
        
        if (!Directory.Exists(appFolder))
            Directory.CreateDirectory(appFolder);

        _configPath = Path.Combine(appFolder, "config.json");
        _config = LoadConfiguration();
    }

    private AppConfiguration LoadConfiguration()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<AppConfiguration>(json) ?? CreateDefaultConfig();
            }
        }
        catch
        {
            // If config is corrupted, use defaults
        }

        return CreateDefaultConfig();
    }

    private AppConfiguration CreateDefaultConfig()
    {
        return new AppConfiguration
        {
            Broker = new BrokerConfiguration
            {
                Host = "192.168.1.100",
                Port = 1883,
                ClientId = "NodoPrincipal"
            },
            Nodes = new List<NodeConfiguration>
            {
                new NodeConfiguration
                {
                    Id = 1,
                    Name = "Sensor A",
                    Enabled = true,
                    Sensors = new List<SensorConfiguration>
                    {
                        new SensorConfiguration { Id = 0, Name = "Temperatura", Unit = "°C", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 10, RawHighWarning = 90 } },
                        new SensorConfiguration { Id = 1, Name = "Presión", Unit = "bar", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 10, RawLowWarning = 1, RawHighWarning = 8 } },
                        new SensorConfiguration { Id = 2, Name = "Flujo", Unit = "L/min", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 5, RawHighWarning = 80 } },
                        new SensorConfiguration { Id = 3, Name = "Nivel", Unit = "%", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 20, RawHighWarning = 90 } }
                    }
                },
                new NodeConfiguration
                {
                    Id = 2,
                    Name = "Sensor B",
                    Enabled = true,
                    Sensors = new List<SensorConfiguration>
                    {
                        new SensorConfiguration { Id = 0, Name = "Temperatura", Unit = "°C", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 10, RawHighWarning = 90 } },
                        new SensorConfiguration { Id = 1, Name = "Presión", Unit = "bar", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 10, RawLowWarning = 1, RawHighWarning = 8 } },
                        new SensorConfiguration { Id = 2, Name = "Flujo", Unit = "L/min", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 5, RawHighWarning = 80 } },
                        new SensorConfiguration { Id = 3, Name = "Nivel", Unit = "%", Limits = new SensorLimits { RawLowAlarm = 0, RawHighAlarm = 100, RawLowWarning = 20, RawHighWarning = 90 } }
                    }
                }
            },
            Alarm = new AlarmConfiguration
            {
                SirenNodeId = 999,
                AutoActivate = true
            },
            System = new SystemConfiguration
            {
                OfflineTimeoutMinutes = 5,
                KeepAliveIntervalSeconds = 60,
                CommandTimeoutSeconds = 10
            }
        };
    }

    public AppConfiguration GetConfiguration() => _config;

    public void SaveConfiguration(AppConfiguration config)
    {
        _config = config;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}