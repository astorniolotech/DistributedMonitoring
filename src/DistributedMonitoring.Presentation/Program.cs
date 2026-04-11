using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using DistributedMonitoring.Application.Services;
using DistributedMonitoring.Domain.Interfaces;
using DistributedMonitoring.Infrastructure;
using DistributedMonitoring.Infrastructure.MQTT;
using DistributedMonitoring.Infrastructure.USB;
using DistributedMonitoring.Infrastructure.Logging;
using DistributedMonitoring.Infrastructure.Configuration;
using DistributedMonitoring.Infrastructure.Protocol;

namespace DistributedMonitoring.Presentation;

static class Program
{
    [STAThread]
    static void Main()
    {
        var services = new ServiceCollection();

        // Infrastructure
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IMessageBus, MessageBus>();
        services.AddSingleton<INodeRepository, NodeRepository>();
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IMqttClientService, MqttClientService>();
        services.AddSingleton<IProtocolParser, ProtocolParser>();
        // FIX: registrar SerialPortService tanto como interfaz como como tipo concreto
        // para que MainForm pueda acceder a metodos especificos (GetAvailablePorts, StartRecordingAsync, etc.)
        services.AddSingleton<SerialPortService>();
        services.AddSingleton<ISerialPortService>(sp => sp.GetRequiredService<SerialPortService>());

        // Application Services
        // FIX: AlarmService debe registrarse antes que NodeService porque NodeService depende de el
        services.AddSingleton<AlarmService>();
        services.AddSingleton<NodeService>();

        var serviceProvider = services.BuildServiceProvider();

        var logService = serviceProvider.GetRequiredService<ILogService>();
        logService.LogSystem("Aplicacion iniciada");

        System.Windows.Forms.ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.Run(new MainForm(serviceProvider));
    }
}
