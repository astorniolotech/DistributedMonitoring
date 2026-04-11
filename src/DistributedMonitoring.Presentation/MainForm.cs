using DistributedMonitoring.Application.Services;
using DistributedMonitoring.Domain.Interfaces;
using DistributedMonitoring.Infrastructure.USB;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;


namespace DistributedMonitoring.Presentation;

public partial class MainForm : Form
{
    private readonly IServiceProvider _serviceProvider;
    private readonly NodeService _nodeService;
    private readonly AlarmService _alarmService;
    private readonly IMqttClientService _mqttService;
    private readonly ILogService _logService;
    private readonly IConfigurationService _configService;
    private readonly ISerialPortService _serialPortService;

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
    private DataGridView? _logsGrid;

    public MainForm(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _nodeService = serviceProvider.GetRequiredService<NodeService>();
        _alarmService = serviceProvider.GetRequiredService<AlarmService>();
        _mqttService = serviceProvider.GetRequiredService<IMqttClientService>();
        _logService = serviceProvider.GetRequiredService<ILogService>();
        _configService = serviceProvider.GetRequiredService<IConfigurationService>();
        _serialPortService = serviceProvider.GetRequiredService<ISerialPortService>();

        InitializeComponent();
        SetupUI();
        LoadNodes();

        // Subscribe to events
        _alarmService.AlarmTriggered += OnAlarmTriggered;
        _alarmService.AlarmSilenced += OnAlarmSilenced;
        _mqttService.MessageReceived += OnMqttMessageReceived;
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
        this.FormClosing += OnFormClosing;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Create main layout
        _mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(10)
        };

        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));   // Menu bar
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));    // Nodes panel
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Alarm panel
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));  // USB panel
        _mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));   // Status bar

        // Menu bar
        var menuPanel = CreateMenuBar();
        _mainLayout.Controls.Add(menuPanel, 0, 0);

        // Nodes panel
        _nodesPanel = CreateNodesPanel();
        _mainLayout.Controls.Add(_nodesPanel, 0, 1);

        // Alarm panel
        _alarmPanel = CreateAlarmPanel();
        _mainLayout.Controls.Add(_alarmPanel, 0, 2);

        // USB panel
        _usbPanel = CreateUsbPanel();
        _mainLayout.Controls.Add(_usbPanel, 0, 3);

        // Status bar
        _statusBar = CreateStatusBar();
        _mainLayout.Controls.Add(_statusBar, 0, 4);

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

        var buttons = new (string Text, EventHandler Handler)[]
        {
            ("Inicializar", OnInitializeNodes),
            ("Configurar", OnConfigure),
            ("Ver Logs", OnViewLogs),
            ("Análisis", OnDataAnalysis),
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
            Padding = new Padding(10)
        };

        var label = new Label
        {
            Text = "NODOS",
            Dock = DockStyle.Top,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 10)
        };

        _nodeCardsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };

        panel.Controls.Add(label);
        panel.Controls.Add(_nodeCardsPanel);
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

        // Row 1: Port control
        var portPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };

        var portLabel = new Label
        {
            Text = "Puerto:",
            AutoSize = true,
            Margin = new Padding(0, 0, 10, 0),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        _portCombo = new ComboBox
        {
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        RefreshPorts();

        _openPortButton = new Button
        {
            Text = "Abrir Puerto",
            Width = 120,
            Margin = new Padding(10, 0, 0, 0)
        };
        _openPortButton.Click += OnOpenPort;

        var refreshPortsBtn = new Button
        {
            Text = "↻",
            Width = 40,
            Margin = new Padding(5, 0, 0, 0)
        };
        refreshPortsBtn.Click += (s, e) => RefreshPorts();

        portPanel.Controls.Add(portLabel);
        portPanel.Controls.Add(_portCombo);
        portPanel.Controls.Add(_openPortButton);
        portPanel.Controls.Add(refreshPortsBtn);

        // Row 2: Recording controls
        var recordPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill
        };

        _startRecordButton = new Button
        {
            Text = "▶ Iniciar Transmisión",
            Width = 160,
            Enabled = false,
            BackColor = Color.FromArgb(0, 100, 0),
            ForeColor = Color.White
        };
        _startRecordButton.FlatAppearance.BorderSize = 0;
        _startRecordButton.Click += OnStartRecording;

        _stopRecordButton = new Button
        {
            Text = "⏹ Detener Transmisión",
            Width = 160,
            Enabled = false,
            BackColor = Color.FromArgb(100, 0, 0),
            ForeColor = Color.White
        };
        _stopRecordButton.FlatAppearance.BorderSize = 0;
        _stopRecordButton.Click += OnStopRecording;

        _recordStatusLabel = new Label
        {
            Text = "Estado: Cerrado | Registros: 0",
            AutoSize = true,
            Margin = new Padding(20, 0, 0, 0),
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft
        };

        recordPanel.Controls.Add(_startRecordButton);
        recordPanel.Controls.Add(_stopRecordButton);
        recordPanel.Controls.Add(_recordStatusLabel);

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
            Text = "Última actualización: --",
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
        
        // Timer for clock update
        var timer = new Timer { Interval = 1000 };
        timer.Tick += (s, e) => timeLabel.Text = DateTime.Now.ToString("HH:mm:ss");
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
        _nodeCardsPanel?.Controls.Clear();

        var nodes = _nodeService.GetAllNodes();
        foreach (var node in nodes)
        {
            var card = CreateNodeCard(node);
            _nodeCardsPanel?.Controls.Add(card);
        }

        UpdateNodeCount();
    }

    private Panel CreateNodeCard(Node node)
    {
        var card = new Panel
        {
            Width = 150,
            Height = 100,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5),
            Tag = node
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

        // Header: node name and status
        var header = new TableLayoutPanel
        {
            RowCount = 1,
            ColumnCount = 2
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        var nameLabel = new Label
        {
            Text = $"{node.Name} (ID:{node.Id})",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };

        var statusDot = new Label
        {
            Text = "●",
            Dock = DockStyle.Fill,
            TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
            Tag = node
        };
        UpdateNodeStatusColor(statusDot, node.Status);

        header.Controls.Add(nameLabel, 0, 0);
        header.Controls.Add(statusDot, 1, 0);

        // Sensor values
        var valuesLabel = new Label
        {
            Text = string.Join("\n", node.Sensors.Select(s => $"{s.Name}: {s.RawValue?.ToString("F1") ?? "--"} {s.Unit}")),
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 8)
        };

        // Actions
        var actionsLayout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight
        };

        var initBtn = new Button
        {
            Text = "Init",
            Width = 45,
            Height = 20,
            Font = new Font("Segoe UI", 7)
        };
        initBtn.Click += (s, e) => OnInitNode(node.Id);

        var valuesBtn = new Button
        {
            Text = "Values",
            Width = 50,
            Height = 20,
            Font = new Font("Segoe UI", 7)
        };
        valuesBtn.Click += (s, e) => OnRequestValues(node.Id);

        actionsLayout.Controls.Add(initBtn);
        actionsLayout.Controls.Add(valuesBtn);

        layout.Controls.Add(header, 0, 0);
        layout.Controls.Add(valuesLabel, 1, 0);
        layout.Controls.Add(actionsLayout, 2, 0);

        card.Controls.Add(layout);
        return card;
    }

    private void UpdateNodeStatusColor(Label dot, NodeStatus status)
    {
        dot.Text = "●";
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
        var nodes = _nodeService.GetAllNodes();
        var activeCount = nodes.Count(n => n.Status == NodeStatus.Active);
        _nodesStatusLabel!.Text = $"Nodos: {activeCount}/{nodes.Count()}";
    }

    private void RefreshPorts()
    {
        _portCombo!.Items.Clear();
        var ports = _serialPortService.GetAvailablePorts();
        _portCombo.Items.AddRange(ports);
        if (ports.Length > 0)
            _portCombo.SelectedIndex = 0;
    }

    // Event handlers
    private async void OnInitializeNodes(object? sender, EventArgs e)
    {
        var nodes = _nodeService.GetAllNodes();
        foreach (var node in nodes)
        {
            if (node.IsEnabled)
            {
                await _nodeService.InitializeNodeAsync(node.Id);
            }
        }
    }

    private void OnConfigure(object? sender, EventArgs e)
    {
        MessageBox.Show("Ventana de configuración - pendiente de implementación", "Configurar", MessageBoxButtons.OK);
    }

    private void OnViewLogs(object? sender, EventArgs e)
    {
        var logs = _logService.GetRecentLogs(100);
        var logText = string.Join("\n", logs.Select(l => $"{l.Timestamp:HH:mm:ss} | {l.Category} | {l.Message}"));
        MessageBox.Show(logText, "Archivo de Novedades", MessageBoxButtons.OK);
    }

    private void OnDataAnalysis(object? sender, EventArgs e)
    {
        MessageBox.Show("Ventana de análisis de datos - pendiente de implementación", "Análisis", MessageBoxButtons.OK);
    }

    private void OnExit(object? sender, EventArgs e)
    {
        Close();
    }

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
        await _alarmService.SilencesSirenAsync();
    }

    private void OnInitNode(int nodeId)
    {
        _ = _nodeService.InitializeNodeAsync(nodeId);
    }

    private void OnRequestValues(int nodeId)
    {
        _ = _nodeService.RequestValuesAsync(nodeId);
    }

    private void OnAlarmTriggered(object? sender, Alarm alarm)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnAlarmTriggered(sender, alarm)));
            return;
        }

        _alarmLabel!.Text = $"⚠️ ALARMA: {alarm.Description}";
        _alarmLabel!.ForeColor = Color.Red;
        _silenceButton!.Enabled = true;
    }

    private void OnAlarmSilenced(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => OnAlarmSilenced(sender, e)));
            return;
        }

        _alarmLabel!.Text = "Alarma silenciada";
        _alarmLabel!.ForeColor = Color.Orange;
        _silenceButton!.Enabled = false;
    }

    private void OnMqttMessageReceived(object? sender, MqttMessageReceivedEventArgs e)
    {
        // Parse and process MQTT messages
        _lastUpdateLabel!.Text = $"Última actualización: {DateTime.Now:HH:mm:ss}";
        
        // TODO: Parse protocol message and update node data
        _ = Task.Run(() => LoadNodes()); // Refresh UI
    }

    private async void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        await _mqttService.DisconnectAsync();
        if (_serialPortService.IsOpen)
        {
            await _serialPortService.CloseAsync();
        }
    }
}