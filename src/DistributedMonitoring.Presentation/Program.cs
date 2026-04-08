using Microsoft.Extensions.DependencyInjection;
using DistributedMonitoring.Application.Services;
using DistributedMonitoring.Domain.Interfaces;
using DistributedMonitoring.Infrastructure;
using DistributedMonitoring.Infrastructure.MQTT;
using DistributedMonitoring.Infrastructure.USB;
using DistributedMonitoring.Infrastructure.Logging;
using DistributedMonitoring.Infrastructure.Configuration;

namespace DistributedMonitoring.Presentation;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Configure services
        var services = new ServiceCollection();
        
        // Infrastructure
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<INodeRepository, NodeRepository>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IMqttClientService, MqttClientService>();
        services.AddSingleton<ISerialPortService, SerialPortService>();
        
        // Application Services
        services.AddSingleton<NodeService>();
        services.AddSingleton<AlarmService>();
        
        // Build provider
        var serviceProvider = services.BuildServiceProvider();
        
        // Initialize logging
        var logService = serviceProvider.GetRequiredService<ILogService>();
        logService.LogSystem("Sistema iniciado");
        
        // Run UI
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(serviceProvider));
    }
}