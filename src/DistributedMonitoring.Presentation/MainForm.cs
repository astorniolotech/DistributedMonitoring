using DistributedMonitoring.Application.Services;
using DistributedMonitoring.Domain.Interfaces;
using DistributedMonitoring.Infrastructure.USB;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Forms;
// FIX: eliminado "using System.Windows.Forms.VisualStyles" que causaba ambiguedad en ContentAlignment

namespace DistributedMonitoring.Presentation;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NodeService _nodeService;
    private readonly AlarmService _alarmService;
    private readonly IMqttClientService _mqttService;
    private readonly ILogService _logService;
    private readonly IConfigurationService _configService;
    private readonly SerialPortService _serialPortService;

    private TableLayoutPanel? _mainLayout;
    private Panel? _nodesPanel;
    private Panel? _alarmPanel;
    private Panel? _usbPanel;
    private Panel? _statusBar;
    private FlowLayoutPanel? _nodeCardsPanel;
    private Label? _alarmLabel;
    private Button? _silenceButton;
    private ComboBox? _portCombo;
    private Button? _openPortButton;
    private Button? _startRecordButton;
    private Button? _stopRecordButton;
    private Label? _recordStatusLabel;
    private Label? _mqttStatusLabel;
    private Label? _nodesStatusLabel;
    private Label? _lastUpdateLabel;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _nodeService = serviceProvider.GetRequiredService<NodeService>();
        _alarmService = serviceProvider.GetRequiredService<AlarmService>();
        _mqttService = serviceProvider.GetRequiredService<IMqttClientService>();
        _logService = serviceProvider.GetRequiredService<ILogService>();
        _configService = serviceProvider.GetRequiredService<IConfigurationService>();
        _serialPortService = serviceProvider.GetRequiredService<SerialPortService>();

        InitializeComponent();
        SetupUI();
        LoadNodes();

        _alarmService.AlarmTriggered += OnAlarmTriggered;
        _alarmService.AlarmSilenced += OnAlarmSilenced;
        _mqttService.MessageReceived += OnMqttMessageReceived;

        // Conectar al broker al iniciar
        _ = _mqttService.ConnectAsync();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(8F, 16F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1200, 700);
        this.Name = "MainForm";
        this.Text = "Sistema de Monitoreo Distribuido";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(37, 37, 38);
        this.ForeColor = Color.White;
        this.FormClosing += OnFormClosing;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(37, 37, 38)
        };

        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        _mainLayout.Controls.Add(CreateMenuBar(), 0, 0);
        _mainLayout.Controls.Add(CreateNodesPanel(), 0, 1);
        _mainLayout.Controls.Add(CreateAlarmPanel(), 0, 2);
        _mainLayout.Controls.Add(CreateUsbPanel(), 0, 3);
        _mainLayout.Controls.Add(CreateStatusBar(), 0, 4);

        this.Controls.Add(_mainLayout);
    }

    private Panel CreateMenuBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10, 5, 10, 5)
        };

        // FIX: tipo explicito (string Text, EventHandler Handler) para evitar ambiguedad del compilador
        var buttons = new (string Text, EventHandler Handler)[]
        {
            ("Inicializar", OnInitializeNodes),
            ("Configurar", OnConfigure),
            ("Ver Logs", OnViewLogs),
            ("Analisis", OnDataAnalysis),
            ("Salir", OnExit)
        };

        foreach (var (text, handler) in buttons)
        {
            var btn = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(63, 63, 70),
                Padding = new Padding(15, 8, 15, 8),
                Margin = new Padding(5)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += handler;
            layout.Controls.Add(btn);
        }

        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateNodesPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(45, 45, 48),
            Padding = new Padding(10)
        };

        var label = new Label
        {
            Text = "NODOS",
            Dock = DockStyle.Top,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 10)
        };

        _nodeCardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            BackColor = Color.FromArgb(45, 45, 48)
        };

        panel.Controls.Add(_nodeCardsPanel);
        panel.Controls.Add(label);
        return panel;
    }

    private Panel CreateAlarmPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(50, 0, 0),
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 2
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        _alarmLabel = new Label
        {
            Text = "Sin alarmas activas",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12),
            // FIX: System.Drawing.ContentAlignment para evitar ambiguedad con VisualStyles
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        _silenceButton = new Button
        {
            Text = "SILENCIAR",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(200, 100, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Enabled = false
        };
        _silenceButton.FlatAppearance.BorderSize = 0;
        _silenceButton.Click += OnSilenceAlarm;

        layout.Controls.Add(_alarmLabel, 0, 0);
        layout.Controls.Add(_silenceButton, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateUsbPanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(45, 45, 48),
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        // Fila 1: control de puerto
        var portPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };

        var portLabel = new Label
        {
            Text = "Puerto:",
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 10, 0),
            // FIX: System.Drawing.ContentAlignment calificado explicitamente
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        _portCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        RefreshPorts();

        _openPortButton = new Button
        {
            Text = "Abrir Puerto",
            Width = 120,
            Margin = new Padding(10, 0, 0, 0)
        };
        _openPortButton.Click += OnOpenPort;

        var refreshPortsBtn = new Button { Text = "↻", Width = 40, Margin = new Padding(5, 0, 0, 0) };
        refreshPortsBtn.Click += (s, e) => RefreshPorts();

        portPanel.Controls.AddRange(new Control[] { portLabel, _portCombo, _openPortButton, refreshPortsBtn });

        // Fila 2: controles de grabacion
        var recordPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };

        _startRecordButton = new Button
        {
            Text = "▶ Iniciar Transmision",
            Width = 160,
            Enabled = false,
            BackColor = Color.FromArgb(0, 100, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _startRecordButton.FlatAppearance.BorderSize = 0;
        _startRecordButton.Click += OnStartRecording;

        _stopRecordButton = new Button
        {
            Text = "⏹ Detener Transmision",
            Width = 160,
            Enabled = false,
            BackColor = Color.FromArgb(100, 0, 0),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _stopRecordButton.FlatAppearance.BorderSize = 0;
        _stopRecordButton.Click += OnStopRecording;

        _recordStatusLabel = new Label
        {
            Text = "Estado: Cerrado | Registros: 0",
            AutoSize = true,
            ForeColor = Color.White,
            Margin = new Padding(20, 0, 0, 0),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        recordPanel.Controls.AddRange(new Control[] { _startRecordButton, _stopRecordButton, _recordStatusLabel });

        layout.Controls.Add(portPanel, 0, 0);
        layout.Controls.Add(recordPanel, 0, 1);
        panel.Controls.Add(layout);
        return panel;
    }

    private Panel CreateStatusBar()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 30, 30)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        _mqttStatusLabel = new Label
        {
            Text = "MQTT: Desconectado",
            ForeColor = Color.LightGray,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        _nodesStatusLabel = new Label
        {
            Text = "Nodos: 0",
            ForeColor = Color.LightGray,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };

        _lastUpdateLabel = new Label
        {
            Text = "Ultima actualizacion: --",
            ForeColor = Color.LightGray,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 10, 0)
        };

        var timeLabel = new Label
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            ForeColor = Color.LightGray,
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter
        };

        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
        {
            timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
            // Actualizar estado MQTT en la barra
            _mqttStatusLabel.Text = _mqttService.IsConnected ? "MQTT: Conectado" : "MQTT: Desconectado";
            _mqttStatusLabel.ForeColor = _mqttService.IsConnected ? Color.LimeGreen : Color.OrangeRed;
        };
        timer.Start();

        layout.Controls.Add(_mqttStatusLabel, 0, 0);
        layout.Controls.Add(_nodesStatusLabel, 1, 0);
        layout.Controls.Add(_lastUpdateLabel, 2, 0);
        layout.Controls.Add(timeLabel, 3, 0);

        panel.Controls.Add(layout);
        return panel;
    }

    private void LoadNodes()
    {
        if (_nodeCardsPanel == null) return;

        _nodeCardsPanel.Controls.Clear();
        var nodes = _nodeService.GetAllNodes();

        foreach (var node in nodes)
        {
            _nodeCardsPanel.Controls.Add(CreateNodeCard(node));
        }

        UpdateNodeCount();
    }

    private Panel CreateNodeCard(Node node)
    {
        var card = new Panel
        {
            Width = 160,
            Height = 110,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(55, 55, 60),
            Margin = new Padding(5),
            Tag = node.Id
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));

        // Header
        var header = new TableLayoutPanel { RowCount = 1, ColumnCount = 2, Dock = DockStyle.Fill };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var nameLabel = new Label
        {
            Text = $"{node.Name} (ID:{node.Id})",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        };

        var statusDot = new Label
        {
            Text = "●",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Tag = node.Id
        };
        UpdateNodeStatusColor(statusDot, node.Status);

        header.Controls.Add(nameLabel, 0, 0);
        header.Controls.Add(statusDot, 1, 0);

        // Valores de sensores
        var valuesLabel = new Label
        {
            Text = string.Join("\n", node.Sensors.Select(s => $"{s.Name}: {s.RawValue?.ToString("F1") ?? "--"} {s.Unit}")),
            Dock = DockStyle.Fill,
            ForeColor = Color.LightGray,
            Font = new Font("Segoe UI", 7)
        };

        // Acciones
        var actionsLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill };

        var initBtn = new Button { Text = "Init", Width = 45, Height = 22, Font = new Font("Segoe UI", 7) };
        initBtn.Click += (s, e) => _ = _nodeService.InitializeNodeAsync(node.Id);

        var valuesBtn = new Button { Text = "Values", Width = 55, Height = 22, Font = new Font("Segoe UI", 7) };
        valuesBtn.Click += (s, e) => _ = _nodeService.RequestValuesAsync(node.Id);

        actionsLayout.Controls.AddRange(new Control[] { initBtn, valuesBtn });

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(valuesLabel, 0, 1);
        layout.Controls.Add(actionsLayout, 0, 2);

        card.Controls.Add(layout);
        return card;
    }

    private void UpdateNodeStatusColor(Label dot, NodeStatus status)
    {
        dot.ForeColor = status switch
        {
            NodeStatus.Active => Color.LimeGreen,
            NodeStatus.Initializing => Color.Orange,
            NodeStatus.Offline => Color.Red,
            _ => Color.Gray
        };
    }

    private void UpdateNodeCount()
    {
        var nodes = _nodeService.GetAllNodes().ToList();
        var activeCount = nodes.Count(n => n.Status == NodeStatus.Active);
        if (_nodesStatusLabel != null)
            _nodesStatusLabel.Text = $"Nodos: {activeCount}/{nodes.Count}";
    }

    private void RefreshPorts()
    {
        if (_portCombo == null) return;
        _portCombo.Items.Clear();
        var ports = _serialPortService.GetAvailablePorts();
        _portCombo.Items.AddRange(ports);
        if (ports.Length > 0)
            _portCombo.SelectedIndex = 0;
    }

    // --- Event handlers ---

    private async void OnInitializeNodes(object? sender, EventArgs e)
    {
        foreach (var node in _nodeService.GetAllNodes().Where(n => n.IsEnabled))
        {
            await _nodeService.InitializeNodeAsync(node.Id);
        }
    }

    private void OnConfigure(object? sender, EventArgs e)
    {
        MessageBox.Show("Ventana de configuracion - pendiente de implementacion", "Configurar", MessageBoxButtons.OK);
    }

    private void OnViewLogs(object? sender, EventArgs e)
    {
        var logs = _logService.GetRecentLogs(100);
        var logText = string.Join("\n", logs.Select(l => $"{l.Timestamp:HH:mm:ss} | {l.Category} | {l.Message}"));
        MessageBox.Show(string.IsNullOrEmpty(logText) ? "Sin eventos registrados." : logText,
            "Archivo de Novedades", MessageBoxButtons.OK);
    }

    private void OnDataAnalysis(object? sender, EventArgs e)
    {
        MessageBox.Show("Ventana de analisis de datos - pendiente de implementacion", "Analisis", MessageBoxButtons.OK);
    }

    private void OnExit(object? sender, EventArgs e) => Close();

    private async void OnOpenPort(object? sender, EventArgs e)
    {
        if (_serialPortService.IsOpen)
        {
            await _serialPortService.CloseAsync();
            _openPortButton!.Text = "Abrir Puerto";
            _startRecordButton!.Enabled = false;
        }
        else
        {
            if (_portCombo!.SelectedItem == null)
            {
                MessageBox.Show("Seleccione un puerto", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await _serialPortService.OpenAsync(_portCombo.SelectedItem.ToString()!);
                _openPortButton!.Text = "Cerrar Puerto";
                _startRecordButton!.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir puerto: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void OnStartRecording(object? sender, EventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"datos_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            await _serialPortService.StartRecordingAsync(dialog.FileName);
            await _serialPortService.SendStartCommandAsync();
            _startRecordButton!.Enabled = false;
            _stopRecordButton!.Enabled = true;
            _recordStatusLabel!.Text = "Estado: Grabando...";
        }
    }

    private async void OnStopRecording(object? sender, EventArgs e)
    {
        await _serialPortService.SendStopCommandAsync();
        await _serialPortService.StopRecordingAsync();
        _startRecordButton!.Enabled = true;
        _stopRecordButton!.Enabled = false;
        _recordStatusLabel!.Text = "Estado: Detenido";
    }

    private async void OnSilenceAlarm(object? sender, EventArgs e)
    {
        await _alarmService.SilenceSirenAsync();
    }

    private void OnAlarmTriggered(object? sender, Alarm alarm)
    {
        // FIX: siempre invocar en el hilo UI correctamente
        if (InvokeRequired) { Invoke(() => OnAlarmTriggered(sender, alarm)); return; }

        _alarmLabel!.Text = $"⚠️ ALARMA: {alarm.Description}";
        _alarmLabel.ForeColor = Color.Red;
        _silenceButton!.Enabled = true;
    }

    private void OnAlarmSilenced(object? sender, EventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnAlarmSilenced(sender, e)); return; }

        _alarmLabel!.Text = "Alarma silenciada";
        _alarmLabel.ForeColor = Color.Orange;
        _silenceButton!.Enabled = false;
    }

    private void OnMqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        if (InvokeRequired) { Invoke(() => OnMqttMessageReceived(sender, e)); return; }

        _lastUpdateLabel!.Text = $"Ultima actualizacion: {DateTime.Now:HH:mm:ss}";

        // FIX: LoadNodes se llama en el hilo UI directamente, no en Task.Run
        LoadNodes();
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        await _mqttService.DisconnectAsync();
        if (_serialPortService.IsOpen)
            await _serialPortService.CloseAsync();
    }
}
