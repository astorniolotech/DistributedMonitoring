using DistributedMonitoring.Domain.Interfaces;
using System.IO.Ports;
using System.Text;

namespace DistributedMonitoring.Infrastructure.USB;

public class SerialPortService : ISerialPortService, IDisposable
{
    private SerialPort? _serialPort;
    private readonly ILogService _logService;
    private bool _isRecording;
    private StreamWriter? _recordingWriter;
    private string? _recordingPath;
    // FIX: eliminado _reconnectTimer que estaba declarado sin uso y causaba error en Dispose()

    public bool IsOpen => _serialPort?.IsOpen ?? false;
    public bool IsRecording => _isRecording;
    public event EventHandler<string>? DataReceived;

    public SerialPortService(ILogService logService)
    {
        _logService = logService;
    }

    public string[] GetAvailablePorts()
    {
        return SerialPort.GetPortNames();
    }

    public async Task OpenAsync(string portName)
    {
        if (_serialPort?.IsOpen == true)
        {
            await CloseAsync();
        }

        _serialPort = new SerialPort(portName)
        {
            BaudRate = 115200,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        _serialPort.DataReceived += OnDataReceived;
        _serialPort.Open();

        _logService.LogSystem($"Puerto serie {portName} abierto");

        await Task.CompletedTask; // mantener firma async para la interfaz
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var data = _serialPort!.ReadExisting();

            if (_isRecording && _recordingWriter != null)
            {
                if (data.Contains('$') && data.Contains('#'))
                {
                    var lines = data.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        if (line.Contains('$') && line.Contains('#'))
                        {
                            _recordingWriter.WriteLine($"{DateTime.Now:O},{line.Trim()}");
                        }
                    }
                }
            }

            DataReceived?.Invoke(this, data);
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error leyendo del puerto serie: {ex.Message}");
        }
    }

    public async Task CloseAsync()
    {
        if (_isRecording)
        {
            await StopRecordingAsync();
        }

        if (_serialPort?.IsOpen == true)
        {
            _serialPort.DataReceived -= OnDataReceived;
            _serialPort.Close();
            _logService.LogSystem("Puerto serie cerrado");
        }
    }

    public async Task SendAsync(string message)
    {
        if (_serialPort?.IsOpen != true)
        {
            _logService.LogCommunication("Puerto serie no abierto");
            return;
        }

        try
        {
            await _serialPort.BaseStream.WriteAsync(Encoding.ASCII.GetBytes(message));
        }
        catch (Exception ex)
        {
            _logService.LogCommunication($"Error enviando al puerto serie: {ex.Message}");
        }
    }

    public async Task StartRecordingAsync(string filePath)
    {
        if (_serialPort?.IsOpen != true)
        {
            _logService.LogCommunication("No se puede grabar - puerto no abierto");
            return;
        }

        _recordingPath = filePath;
        _recordingWriter = new StreamWriter(filePath, append: true);

        await _recordingWriter.WriteLineAsync("Timestamp,NodeId,Sensor1,Sensor2,Sensor3,Sensor4");

        _isRecording = true;
        _logService.LogSystem($"Grabacion iniciada: {filePath}");
    }

    public async Task StopRecordingAsync()
    {
        if (!_isRecording) return;

        _isRecording = false;

        if (_recordingWriter != null)
        {
            await _recordingWriter.FlushAsync();
            _recordingWriter.Close();
            _recordingWriter = null;
        }

        _logService.LogSystem($"Grabacion finalizada: {_recordingPath}");
    }

    public async Task SendStartCommandAsync()
    {
        await SendAsync("EMPEZAR#");
        _logService.LogOperator("Comando EMPEZAR enviado");
    }

    public async Task SendStopCommandAsync()
    {
        await SendAsync("FINALIZAR#");
        _logService.LogOperator("Comando FINALIZAR enviado");
    }

    public void Dispose()
    {
        _recordingWriter?.Dispose();
        _serialPort?.Dispose();
    }
}
