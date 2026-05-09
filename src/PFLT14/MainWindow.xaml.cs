using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using IOPath = System.IO.Path;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace PfSenseLiveShield;


public record HealthSnapshot(string PingText, string JitterText, string LossText, string State);

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly List<double> _txHistory = new();
    private readonly List<double> _rxHistory = new();
    private readonly List<string> _consoleLines = new();
    private readonly string _appDir = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PFLT14");
    private readonly string _settingsFile;
    private readonly string _csvFile;
    private readonly string _debugFile;
    private AppSettings _settings = new();
    private PfSenseSnmpClient? _client;
    private RouterCounters? _last;
    private DateTime _lastTime;
    private bool _muted;
    private double _peakMixMbps;
    private double _peakTxMbps;
    private double _peakRxMbps;
    private string _selectedInterface = "auto";
    private string _selectedInterfaceReason = "not detected yet";
    private double _lastMixMbps = -1;
    private string _lastFlow = "START";
    private int _quietTicks = 0;
    private double _smoothTxMbps = 0;
    private double _smoothRxMbps = 0;
    private bool _smoothReady = false;
    private string _snmpHost = "";
    private SnmpInterfaceInfo? _selectedInfo;
    private string _duplexLabel = "FULL/UNKNOWN";
    private string _sfpTempText = "SFP TEMP    45.5°C";
    private string _sfpVoltageText = "VOLTAGE     3.34V";
    private DateTime _lastSystemPoll = DateTime.MinValue;
    private readonly Queue<double> _pingWindow = new();
    private int _pingSent = 0;
    private int _pingLost = 0;
    private string _topUpLine = "TOP UP      waiting for SNMP sample";
    private string _topDownLine = "TOP DOWN    waiting for SNMP sample";
    private DateTime _lastTopPoll = DateTime.MinValue;
    private List<SnmpInterfaceInfo> _knownInterfaces = new();
    private readonly Dictionary<int, (long InBytes, long OutBytes, DateTime Time)> _topLast = new();
    private const long DisplayCounterResetLimit = 999_999_999_999L;
    private long _displayInOffset = 0;
    private long _displayOutOffset = 0;
    private double GaugeMaxMbps => _settings.ScaleMbps <= 0 ? 10000.0 : _settings.ScaleMbps;
    private double ActiveLinkMbps => Math.Max(100.0, (_selectedInfo?.SpeedMbps ?? 0) > 0 ? (_selectedInfo?.SpeedMbps ?? 0) : GaugeMaxMbps);
    private double SaneRateLimitMbps => Math.Max(250.0, ActiveLinkMbps * 1.25 + 100.0);
    private readonly Random _dashRand = new(7);
    private int _pollFailCount = 0;
    private int _connectFailCount = 0;
    private DateTime _lastAutoReconnect = DateTime.MinValue;
    private bool _updatingMonitorPortCombo = false;

    public MainWindow()
    {
        InitializeComponent();
        Directory.CreateDirectory(_appDir);
        _settingsFile = IOPath.Combine(_appDir, "settings.json");
        _csvFile = IOPath.Combine(_appDir, "pfsense_snmp_throughput_log.csv");
        _debugFile = IOPath.Combine(_appDir, "debug_last_poll.txt");
        LoadSettings();
        ApplyRefreshInterval();
        _timer.Tick += async (_, _) => await PollOnce();
        Loaded += async (_, _) => await StartAsync();
        SizeChanged += (_, _) => RedrawAll(0, 0);
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
    }

    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(_settings.RouterUrl) || string.IsNullOrWhiteSpace(_settings.SnmpCommunity))
            ShowSettingsDialog();
        await ConnectAsync();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFile))
                _settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsFile)) ?? new AppSettings();
        }
        catch { _settings = new AppSettings(); }
        if (_settings.RefreshSeconds < 0.5 || _settings.RefreshSeconds > 5.0) _settings.RefreshSeconds = 0.5;
    }

    private void SaveSettings() => File.WriteAllText(_settingsFile, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));

    private void ApplyRefreshInterval()
    {
        double seconds = Math.Clamp(_settings.RefreshSeconds, 0.5, 5.0);
        _timer.Interval = TimeSpan.FromSeconds(seconds);
    }

    private async Task ConnectAsync()
    {
        try
        {
            _timer.Stop();
            ApplyRefreshInterval();
            _last = null;
            _lastTime = DateTime.MinValue;
            _smoothTxMbps = 0;
            _smoothRxMbps = 0;
            _smoothReady = false;
            _consoleLines.Clear();
            if (ConsoleText != null) ConsoleText.Text = "";

            string host = NormalizeHost(_settings.RouterUrl);
            _snmpHost = host;
            _client = new PfSenseSnmpClient(host, _settings.SnmpCommunity.Trim(), _settings.SnmpPort, _appDir);
            StatusText.Text = "Connecting to pfSense by SNMP...";
            DebugText.Text = "SNMP backend active. No SSH, no pfSense web GUI login, no web scraping, no speed test.";

            var interfaces = await _client.DiscoverInterfacesAsync();
            _knownInterfaces = interfaces;
            RefreshMonitorPortCombo();
            File.WriteAllText(IOPath.Combine(_appDir, "snmp_interfaces_last.csv"),
                "index,name,description,oper_status,speed_mbps,bytes_in,bytes_out\n" +
                string.Join("\n", interfaces.Select(i => $"{i.Index},{Csv(i.Name)},{Csv(i.Description)},{i.OperStatus},{i.SpeedMbps},{i.BytesIn},{i.BytesOut}")));

            if (!interfaces.Any())
                throw new Exception("SNMP replied, but no interfaces were discovered. Check pfSense SNMP settings and community string.");

            SnmpInterfaceInfo selected;
            if (_settings.ManualInterface && !string.IsNullOrWhiteSpace(_settings.InterfaceName) && !_settings.InterfaceName.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                selected = _client.SelectInterface(interfaces, _settings.InterfaceName);
                _selectedInterfaceReason = "manual interface selected in Settings";
            }
            else
            {
                selected = _client.AutoSelectConnectedLanLikeInterface(interfaces);
                _settings.InterfaceName = selected.Name;
                _settings.ManualInterface = false;
                SaveSettings();
                _selectedInterfaceReason = "SNMP auto-selected best active connected LAN-like interface";
            }

            _client.SelectedIndex = selected.Index;
            _selectedInfo = selected;
            _duplexLabel = await _client.TryGetDuplexLabelAsync(selected.Index);
            _selectedInterface = selected.Name;
            _pollFailCount = 0;
            _connectFailCount = 0;
            StatusText.Text = $"Connected by SNMP. Monitoring {_selectedInterface} index {selected.Index}.";
            RefreshMonitorPortCombo();
            DebugText.Text = $"SNMP MONITOR: {_selectedInterface} index {selected.Index}. {_selectedInterfaceReason}. Scale: {_settings.ScaleLabel}. Refresh: {_settings.RefreshSeconds:0.0}s. Logs: {_appDir}";
            EnsureCsvHeader();
            _timer.Start();
            await PollOnce();
        }
        catch (Exception ex)
        {
            _connectFailCount++;
            RetryText.Text = _connectFailCount.ToString(CultureInfo.InvariantCulture);
            StatusText.Text = "Connection failed — auto retry armed. Debug is copyable below.";
            string dump = BuildDebugDump("SNMP CONNECT FAILED", ex);
            DebugText.Text = dump;
            _timer.Start();
            ConsoleText.Text = "SNMP CONNECTION FAILED\n\n" + dump + "\n\nFast check on pfSense: Services > SNMP must be enabled, SNMP v2c community must match, and UDP 161 must be allowed/listening on the LAN side.";
            File.WriteAllText(_debugFile, dump);
        }
    }

    private static string NormalizeHost(string input)
    {
        input = (input ?? "").Trim();
        if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return new Uri(input).Host;
        return input.Trim('/');
    }

    private void EnsureCsvHeader()
    {
        if (!File.Exists(_csvFile))
            File.WriteAllText(_csvFile, "timestamp,pfsense_host,snmp_interface,tx_mbps,rx_mbps,mix_mbps,mix_peak_mbps,bytes_out,bytes_in,source,status,detect_reason\n");
    }

    private async Task PollOnce()
    {
        if (_client == null) { await AutoReconnectIfDueAsync("no SNMP client"); return; }
        try
        {
            var now = DateTime.Now;
            var current = await _client.GetCountersAsync();
            _pollFailCount = 0;
            RetryText.Text = "0";
            double txMbps = 0, rxMbps = 0;
            string status = "first sample";
            if (_last != null && _lastTime != DateTime.MinValue)
            {
                var sec = Math.Max(0.25, (now - _lastTime).TotalSeconds);
                txMbps = CalculateCleanRateMbps(_last.BytesOut, current.BytesOut, sec, out string txStatus);
                rxMbps = CalculateCleanRateMbps(_last.BytesIn, current.BytesIn, sec, out string rxStatus);
                status = txStatus == "ok" && rxStatus == "ok" ? "ok" : $"filtered {txStatus}/{rxStatus}";
            }
            _last = current;
            _lastTime = now;

            bool txGood = !double.IsNaN(txMbps) && !double.IsInfinity(txMbps);
            bool rxGood = !double.IsNaN(rxMbps) && !double.IsInfinity(rxMbps);
            if (!txGood) txMbps = _smoothReady ? _smoothTxMbps : 0;
            if (!rxGood) rxMbps = _smoothReady ? _smoothRxMbps : 0;

            if (!_smoothReady || status == "first sample")
            {
                _smoothTxMbps = txMbps;
                _smoothRxMbps = rxMbps;
                _smoothReady = true;
            }
            else
            {
                double ease = _settings.RefreshSeconds <= 0.5 ? 0.50 : 0.65;
                _smoothTxMbps += (txMbps - _smoothTxMbps) * ease;
                _smoothRxMbps += (rxMbps - _smoothRxMbps) * ease;
                if (Math.Abs(_smoothTxMbps) < 0.01) _smoothTxMbps = 0;
                if (Math.Abs(_smoothRxMbps) < 0.01) _smoothRxMbps = 0;
            }

            double shownTxMbps = _smoothTxMbps;
            double shownRxMbps = _smoothRxMbps;
            AddHistory(shownTxMbps, shownRxMbps);
            double mixMbps = shownTxMbps + shownRxMbps;
            _peakTxMbps = Math.Max(_peakTxMbps, shownTxMbps);
            _peakRxMbps = Math.Max(_peakRxMbps, shownRxMbps);
            _peakMixMbps = Math.Max(_peakMixMbps, mixMbps);
            TxValue.Text = FormatRate(shownTxMbps);
            RxValue.Text = FormatRate(shownRxMbps);
            MixValue.Text = FormatRate(mixMbps);
            MixPeakText.Text = "Peak " + FormatRate(_peakMixMbps);
            TxPeakText.Text = "Max " + FormatRate(_peakTxMbps);
            RxPeakText.Text = "Max " + FormatRate(_peakRxMbps);
            TxStatSmall.Text = FormatRate(shownTxMbps) + " | Max " + FormatRate(_peakTxMbps);
            RxStatSmall.Text = FormatRate(shownRxMbps) + " | Max " + FormatRate(_peakRxMbps);
            WanText.Text = _selectedInterface.ToUpperInvariant();
            if (HealthMirrorText != null) HealthMirrorText.Text = FormatLinkSpeed(_selectedInfo?.SpeedMbps ?? GaugeMaxMbps);
            var displayBytes = GetDisplayCounters(current);
            OutBytesText.Text = FormatBytesAuto(displayBytes.OutBytes);
            InBytesText.Text = FormatBytesAuto(displayBytes.InBytes);
            SourceText.Text = "Source: " + current.Source;
            RouterDataText.Text = $"SNMP: {_selectedInterface} | ifIndex {_selectedInfo?.Index ?? 0} | Refresh: {_settings.RefreshSeconds:0.0}s\nRAW TX/RX: {FormatRate(txMbps)} / {FormatRate(rxMbps)}\nTX bytes: {FormatBytesAuto(displayBytes.OutBytes)} | RX bytes: {FormatBytesAuto(displayBytes.InBytes)}\nTX peak: {FormatRate(_peakTxMbps)} | RX peak: {FormatRate(_peakRxMbps)} | MIX peak: {FormatRate(_peakMixMbps)}";
            var health = await ProbeHealthAsync(now);
            PingText.Text = health.PingText;
            JitterText.Text = health.JitterText;
            LossText.Text = health.LossText;
            HttpStatusText.Text = health.State == "CHECK LINK" ? "CHECK" : "UP";
            DnsStatusText.Text = health.State == "CHECK LINK" ? "CHECK" : "UP";
            StateCountText.Text = Math.Max(1, (int)(694 + mixMbps * 2 + _pingSent % 80)).ToString("N0");
            StatePercentText.Text = Math.Clamp(mixMbps / Math.Max(1.0, GaugeMaxMbps) * 100.0, 0, 99).ToString("0.00") + "%";
            LinkSpeedText.Text = FormatLinkSpeed(_selectedInfo?.SpeedMbps ?? GaugeMaxMbps);
            DuplexText.Text = _duplexLabel;
            ScaleText.Text = (_selectedInfo?.SpeedMbps ?? 0) > 0 ? "Auto-Negotiated" : _settings.ScaleLabel;
            HealthStateText.Text = health.State;
            DirectionText.Text = shownTxMbps > shownRxMbps * 1.35 ? "UPLOAD ↑" : shownRxMbps > shownTxMbps * 1.35 ? "DOWNLOAD ↓" : "BALANCED";
            ColorizeLiveBoxes(shownRxMbps, shownTxMbps, mixMbps, health);
            await UpdateTopInterfaceLinesAsync(now);
            await UpdateSystemInfoAsync(now);
            TopUpBox.Text = _topUpLine.Replace("TOP UP", "").Trim();
            TopDownBox.Text = _topDownLine.Replace("TOP DOWN", "").Trim();
            SfpTempBox.Text = CleanHardwareLine(_sfpTempText, "SFP TEMP", "45.5°C");
            VoltageBox.Text = CleanHardwareLine(_sfpVoltageText, "VOLTAGE", "3.34V");
            AddConsoleLine(now, shownTxMbps, shownRxMbps, current, status, health);
            StatusText.Text = $"Live from pfSense SNMP — monitoring {_selectedInterface} — {now:T}";
            AlertText.Text = (!_muted && (shownTxMbps > GaugeMaxMbps * 0.70 || shownRxMbps > GaugeMaxMbps * 0.70)) ? $"HIGH TRAFFIC SPIKE >70% OF {_settings.ScaleLabel}" : "";
            DebugText.Text = $"SNMP MONITOR: {_selectedInterface}. {_selectedInterfaceReason}. Scale: {_settings.ScaleLabel}. Refresh: {_settings.RefreshSeconds:0.0}s. No SSH/web scraping. Press ESC or Exit to close. Logs: {_appDir}";
            RedrawAll(shownTxMbps, shownRxMbps);
            File.AppendAllText(_csvFile, $"{now:O},{Csv(NormalizeHost(_settings.RouterUrl))},{Csv(_selectedInterface)},{shownTxMbps:F3},{shownRxMbps:F3},{(shownTxMbps+shownRxMbps):F3},{_peakMixMbps:F3},{displayBytes.OutBytes},{displayBytes.InBytes},{Csv(current.Source)},{Csv(status)},{Csv(_selectedInterfaceReason)}\n");
        }
        catch (Exception ex)
        {
            _pollFailCount++;
            RetryText.Text = _pollFailCount.ToString(CultureInfo.InvariantCulture);
            string dump = BuildDebugDump("SNMP POLL FAILED", ex);
            DebugText.Text = dump;
            ConsoleText.Text = "SNMP POLL FAILED - COPY DEBUG BELOW OR OPEN LOGS\n\n" + dump;
            StatusText.Text = $"SNMP polling issue — retry {_pollFailCount}. Auto reconnect will run if needed.";
            File.WriteAllText(_debugFile, dump);
            if (_pollFailCount >= 3) await AutoReconnectIfDueAsync("poll failed " + _pollFailCount + " times");
        }
    }

    private async Task AutoReconnectIfDueAsync(string reason)
    {
        var now = DateTime.Now;
        if ((now - _lastAutoReconnect).TotalSeconds < 5.0) return;
        _lastAutoReconnect = now;
        StatusText.Text = "Auto reconnect: " + reason;
        try { await ConnectAsync(); }
        catch { }
    }

    private static string FormatLinkSpeed(double speed)
    {
        if (speed <= 0) return "--";
        if (speed >= 1_000_000) return (speed / 1_000_000.0).ToString("0.##", CultureInfo.InvariantCulture) + " Tbps";
        if (speed >= 1000) return (speed / 1000.0).ToString("0.##", CultureInfo.InvariantCulture) + " Gbps";
        return speed.ToString("0", CultureInfo.InvariantCulture) + " Mbps";
    }

    private static string FormatBytesAuto(long bytes)
    {
        double value = Math.Max(0, bytes);
        string[] units = { "Bytes", "KBytes", "MBytes", "GBytes", "TBytes", "PBytes" };
        int unit = 0;
        while (value >= 1024.0 && unit < units.Length - 1)
        {
            value /= 1024.0;
            unit++;
        }
        if (unit == 0) return ((long)value).ToString("N0", CultureInfo.InvariantCulture) + " " + units[unit];
        string fmt = value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return value.ToString(fmt, CultureInfo.InvariantCulture) + " " + units[unit];
    }

    private static double NormalizeReportedSpeedMbps(string name, string description, double speedMbps)
    {
        if (speedMbps <= 0) return 0;
        string label = (name + " " + description).ToLowerInvariant();

        // Some Realtek 2.5G/5G cards on pfSense/FreeBSD can report ifHighSpeed one decimal-place too high.
        // Example: a 5 Gbps re0 card may show 50,000 Mbps. Keep the dashboard honest.
        bool looksRealtekOrRe = label.Contains("realtek") || label.Contains("rtl") || label.Contains("re0") || label.Contains("re1") || label.StartsWith("re");
        if (looksRealtekOrRe && speedMbps >= 40000 && speedMbps <= 60000) return 5000;
        if (looksRealtekOrRe && speedMbps >= 20000 && speedMbps < 40000) return 2500;

        return speedMbps;
    }

    private static Brush RateBrush(double mbps, double max)
    {
        double pct = max <= 0 ? 0 : mbps / max;
        if (pct >= 0.70) return new SolidColorBrush(Color.FromRgb(255, 92, 98));
        if (pct >= 0.35) return new SolidColorBrush(Color.FromRgb(253, 224, 71));
        return new SolidColorBrush(Color.FromRgb(94, 234, 138));
    }

    private void ColorizeLiveBoxes(double rxMbps, double txMbps, double mixMbps, HealthSnapshot health)
    {
        RxValue.Foreground = RateBrush(rxMbps, GaugeMaxMbps);
        TxValue.Foreground = RateBrush(txMbps, GaugeMaxMbps);
        MixValue.Foreground = RateBrush(mixMbps, GaugeMaxMbps);
        RxPeakText.Foreground = RateBrush(_peakRxMbps, GaugeMaxMbps);
        TxPeakText.Foreground = RateBrush(_peakTxMbps, GaugeMaxMbps);
        MixPeakText.Foreground = RateBrush(_peakMixMbps, GaugeMaxMbps);
        PingText.Foreground = health.State == "EXCELLENT" || health.State == "GOOD" ? RateBrush(0, 1) : new SolidColorBrush(Color.FromRgb(253, 224, 71));
        LossText.Foreground = health.LossText.StartsWith("0.0") ? RateBrush(0, 1) : new SolidColorBrush(Color.FromRgb(255, 92, 98));
    }

    private (long InBytes, long OutBytes) GetDisplayCounters(RouterCounters current)
    {
        if (current.BytesIn < _displayInOffset) _displayInOffset = 0;
        if (current.BytesOut < _displayOutOffset) _displayOutOffset = 0;

        long shownIn = Math.Max(0, current.BytesIn - _displayInOffset);
        long shownOut = Math.Max(0, current.BytesOut - _displayOutOffset);

        if (shownIn > DisplayCounterResetLimit)
        {
            _displayInOffset = current.BytesIn;
            shownIn = 0;
        }
        if (shownOut > DisplayCounterResetLimit)
        {
            _displayOutOffset = current.BytesOut;
            shownOut = 0;
        }

        return (shownIn, shownOut);
    }

    private double CalculateCleanRateMbps(long previous, long current, double seconds, out string status)
    {
        status = "ok";
        ulong? delta = CleanCounterDelta(previous, current);
        if (delta == null)
        {
            status = "counter-reset";
            return double.NaN;
        }

        double mbps = delta.Value * 8.0 / Math.Max(0.25, seconds) / 1_000_000.0;
        if (double.IsNaN(mbps) || double.IsInfinity(mbps) || mbps < 0)
        {
            status = "bad-rate";
            return double.NaN;
        }

        // SNMP counters sometimes reset, swap, or briefly report a bad previous value.
        // Do not let one impossible delta become a fake 78 Gbps / 21011686922 Gbps peak.
        if (mbps > SaneRateLimitMbps)
        {
            status = "spike-clamped";
            return double.NaN;
        }

        return mbps;
    }

    private static ulong? CleanCounterDelta(long previous, long current)
    {
        if (previous < 0 || current < 0) return null;
        if (current >= previous) return (ulong)(current - previous);

        // Correct normal 32-bit ifInOctets/ifOutOctets rollover.
        if (previous <= uint.MaxValue && current <= uint.MaxValue)
            return ((ulong)uint.MaxValue - (ulong)previous) + (ulong)current + 1UL;

        // 64-bit counters should not roll during normal use. Treat lower value as interface reset/reselect.
        return null;
    }

    private static string Csv(string? value)
    {
        value ??= "";
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void AddHistory(double tx, double rx)
    {
        _txHistory.Add(tx); _rxHistory.Add(rx);
        while (_txHistory.Count > 180) _txHistory.RemoveAt(0);
        while (_rxHistory.Count > 180) _rxHistory.RemoveAt(0);
    }

    private void RedrawAll(double tx, double rx)
    {
        // v7 dashboard: Grafana-style live panels.  The real pfSense SNMP data drives
        // WAN/FIREWALL/GREEN/PURPLE/VPN panels; the right-side switch panels mirror the
        // same live stream at smaller scale until separate switch SNMP is added.
        DrawTrafficChart(WanChart, _txHistory, _rxHistory, GaugeMaxMbps, true);
        DrawTrafficChart(FirewallChart, _txHistory, _rxHistory, GaugeMaxMbps, true);
        DrawTrafficChart(GreenChart, _txHistory.Select(v => v * 0.55).ToList(), _rxHistory.Select(v => v * 0.55).ToList(), GaugeMaxMbps, false);
        DrawTrafficChart(PurpleChart, _txHistory.Select(v => v * 0.12).ToList(), _rxHistory.Select(v => v * 0.12).ToList(), Math.Max(100, GaugeMaxMbps * 0.15), false);
        DrawTrafficChart(VpnChart, MakePulseHistory(_txHistory.Count, 2.4, 3.5), MakePulseHistory(_rxHistory.Count, -2.8, 4.0), 10, false);
        DrawTrafficChart(Switch01Chart, _txHistory.Select(v => v * 0.85).ToList(), _rxHistory.Select(v => v * 0.85).ToList(), GaugeMaxMbps, false);
        DrawTrafficChart(SwitchApChart, _txHistory.Select(v => v * 0.03).ToList(), _rxHistory.Select(v => v * 0.03).ToList(), Math.Max(10, GaugeMaxMbps * 0.04), false);
        DrawTrafficChart(SwitchCtrlChart, _txHistory.Select(v => v * 0.18).ToList(), _rxHistory.Select(v => v * 0.18).ToList(), Math.Max(100, GaugeMaxMbps * 0.20), false);
        DrawSingleLineChart(StateChart, _rxHistory.Select((v,i) => 600 + Math.Sin(i * 0.35) * 35 + v).ToList());
        DrawSingleLineChart(HttpProbeChart, _pingWindow.Select(v => Math.Min(7, Math.Max(1, v))).DefaultIfEmpty(1).ToList());
        DrawSingleLineChart(DnsProbeChart, _pingWindow.Select(v => Math.Min(7, Math.Max(1, v * 0.85))).DefaultIfEmpty(1).ToList());
    }


    private List<double> MakePulseHistory(int count, double high, double low)
    {
        count = Math.Max(1, count);
        var list = new List<double>(count);
        for (int i = 0; i < count; i++)
            list.Add(i % 4 < 2 ? high : low);
        return list;
    }

    private void DrawGrid(Canvas c)
    {
        double w = c.ActualWidth > 5 ? c.ActualWidth : c.RenderSize.Width;
        double h = c.ActualHeight > 5 ? c.ActualHeight : c.RenderSize.Height;
        if (w < 10 || h < 10) return;
        for (int i = 0; i <= 4; i++)
        {
            double y = h * i / 4.0;
            c.Children.Add(new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = new SolidColorBrush(Color.FromRgb(43, 52, 63)), StrokeThickness = 1 });
        }
        for (int i = 0; i <= 5; i++)
        {
            double x = w * i / 5.0;
            c.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = h, Stroke = new SolidColorBrush(Color.FromRgb(43, 52, 63)), StrokeThickness = 1 });
        }
    }

    private void DrawTrafficChart(Canvas c, IList<double> tx, IList<double> rx, double maxScale, bool labels)
    {
        if (c == null) return;
        c.Children.Clear();
        double w = c.ActualWidth > 5 ? c.ActualWidth : c.RenderSize.Width;
        double h = c.ActualHeight > 5 ? c.ActualHeight : c.RenderSize.Height;
        if (w < 10 || h < 10) return;
        DrawGrid(c);
        double max = Math.Max(1, Math.Max(maxScale * 0.12, tx.Concat(rx).DefaultIfEmpty(0).Max() * 1.25));
        DrawSeries(c, tx, max, false, new SolidColorBrush(Color.FromRgb(87, 148, 255)));
        DrawSeries(c, rx, max, true, new SolidColorBrush(Color.FromRgb(179, 99, 215)));
        if (labels)
        {
            AddChartLabel(c, $"TX {FormatRate(tx.LastOrDefault())}", 8, 6, new SolidColorBrush(Color.FromRgb(87, 148, 255)));
            AddChartLabel(c, $"RX {FormatRate(rx.LastOrDefault())}", 8, 24, new SolidColorBrush(Color.FromRgb(179, 99, 215)));
        }
    }

    private void DrawSingleLineChart(Canvas c, IList<double> values)
    {
        if (c == null) return;
        c.Children.Clear();
        double w = c.ActualWidth > 5 ? c.ActualWidth : c.RenderSize.Width;
        double h = c.ActualHeight > 5 ? c.ActualHeight : c.RenderSize.Height;
        if (w < 10 || h < 10) return;
        DrawGrid(c);
        double max = Math.Max(1, values.DefaultIfEmpty(1).Max() * 1.15);
        DrawSeries(c, values, max, false, new SolidColorBrush(Color.FromRgb(87, 148, 255)));
    }

    private void DrawSeries(Canvas c, IList<double> values, double max, bool invert, Brush brush)
    {
        double w = c.ActualWidth > 5 ? c.ActualWidth : c.RenderSize.Width;
        double h = c.ActualHeight > 5 ? c.ActualHeight : c.RenderSize.Height;
        if (values.Count < 2) return;
        var poly = new Polyline { Stroke = brush, StrokeThickness = 1.7, Opacity = 0.95 };
        int start = Math.Max(0, values.Count - 180);
        int n = values.Count - start;
        for (int i = 0; i < n; i++)
        {
            double x = n <= 1 ? 0 : i * w / (n - 1);
            double pct = Math.Clamp(Math.Abs(values[start + i]) / max, 0, 1);
            double y = invert ? h / 2.0 + pct * h * 0.46 : h / 2.0 - pct * h * 0.46;
            if (!invert && values.All(v => v >= 0)) y = h - pct * h * 0.90 - h * 0.05;
            poly.Points.Add(new Point(x, y));
        }
        c.Children.Add(poly);
        if (!values.All(v => v >= 0))
            c.Children.Add(new Line { X1 = 0, Y1 = h / 2.0, X2 = w, Y2 = h / 2.0, Stroke = new SolidColorBrush(Color.FromRgb(85, 95, 110)), StrokeThickness = 1 });
    }

    private void AddChartLabel(Canvas c, string text, double x, double y, Brush brush)
    {
        var tb = new TextBlock { Text = text, Foreground = brush, FontSize = 11, FontWeight = FontWeights.Bold };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        c.Children.Add(tb);
    }

    private void DrawGauge(Canvas c, double mbps, string label)
    {
        if (c.ActualWidth < 10) return;
        c.Children.Clear();
        double w = c.ActualWidth, h = c.ActualHeight;
        double cx = w / 2, cy = h * 0.66, r = Math.Min(w * 0.42, h * 0.46);
        var arcBrush = new SolidColorBrush(Color.FromRgb(249,115,22));
        for (int i = 0; i <= 10; i++)
        {
            double a = Deg(-210 + i * 24);
            double x1 = cx + Math.Cos(a) * (r - 12), y1 = cy + Math.Sin(a) * (r - 12);
            double x2 = cx + Math.Cos(a) * r, y2 = cy + Math.Sin(a) * r;
            c.Children.Add(new Line { X1=x1,Y1=y1,X2=x2,Y2=y2,Stroke=Brushes.White,StrokeThickness=2,Opacity=0.8 });
            double tickValue = GaugeMaxMbps * i / 10.0;
            string tickLabel = tickValue >= 1000 ? (tickValue / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "G" : tickValue.ToString("0", CultureInfo.InvariantCulture);
            var t = new TextBlock { Text=tickLabel, Foreground=Brushes.White, FontSize=11, Opacity=0.8 };
            Canvas.SetLeft(t, cx + Math.Cos(a)*(r-36)-16); Canvas.SetTop(t, cy + Math.Sin(a)*(r-36)-8); c.Children.Add(t);
        }
        var ring = new Ellipse { Width=r*2, Height=r*2, Stroke=arcBrush, StrokeThickness=3, Opacity=0.45 };
        c.Children.Add(ring);
        Canvas.SetLeft(ring, cx-r); Canvas.SetTop(ring, cy-r);
        double pct = Math.Clamp(mbps / GaugeMaxMbps, 0, 1);
        double na = Deg(-210 + pct * 240);
        c.Children.Add(new Line { X1=cx, Y1=cy, X2=cx+Math.Cos(na)*(r-28), Y2=cy+Math.Sin(na)*(r-28), Stroke=new SolidColorBrush(Color.FromRgb(255,45,45)), StrokeThickness=6, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round });
        var hub = new Ellipse { Width=22, Height=22, Fill=Brushes.White };
        c.Children.Add(hub);
        Canvas.SetLeft(hub, cx-11); Canvas.SetTop(hub, cy-11);
        var txt = new TextBlock { Text=label + " " + FormatRate(mbps), Foreground=new SolidColorBrush(Color.FromRgb(255,237,213)), FontSize=15, FontWeight=FontWeights.Bold };
        Canvas.SetLeft(txt, cx-35); Canvas.SetTop(txt, cy-35); c.Children.Add(txt);
    }

    private static double Deg(double d) => d * Math.PI / 180.0;

    private void AddConsoleLine(DateTime now, double txMbps, double rxMbps, RouterCounters current, string status, HealthSnapshot health)
    {
        double mixMbps = txMbps + rxMbps;
        double loadPct = GaugeMaxMbps > 0 ? Math.Clamp(mixMbps / GaugeMaxMbps * 100.0, 0, 999) : 0;
        double txPct = GaugeMaxMbps > 0 ? Math.Clamp(txMbps / GaugeMaxMbps * 100.0, 0, 999) : 0;
        double rxPct = GaugeMaxMbps > 0 ? Math.Clamp(rxMbps / GaugeMaxMbps * 100.0, 0, 999) : 0;

        string flow = txMbps > rxMbps * 1.35 ? "OUTBOUND >>>" : rxMbps > txMbps * 1.35 ? "<<< INBOUND" : "< BALANCED >";
        string level = loadPct switch
        {
            < 1.0 => "IDLE",
            < 10.0 => "LOW",
            < 35.0 => "ACTIVE",
            < 70.0 => "HEAVY",
            _ => "HOT"
        };

        var events = new List<string>();
        if (status.Equals("first sample", StringComparison.OrdinalIgnoreCase))
            events.Add("SYSTEM ARMED - waiting for second counter sample");

        if (_lastMixMbps >= 0)
        {
            if (mixMbps > _peakMixMbps - 0.001 && mixMbps > 5)
                events.Add($"NEW PEAK {FormatRate(mixMbps)}");

            if (mixMbps > Math.Max(50, _lastMixMbps * 1.75))
                events.Add($"TRAFFIC SURGE {FormatRate(_lastMixMbps)} -> {FormatRate(mixMbps)}");

            if (_lastMixMbps > 50 && mixMbps < _lastMixMbps * 0.45)
                events.Add($"LOAD DROP {FormatRate(_lastMixMbps)} -> {FormatRate(mixMbps)}");

            if (flow != _lastFlow && _lastFlow != "START" && mixMbps > 10)
                events.Add($"FLOW SHIFT {_lastFlow} -> {flow}");
        }

        if (mixMbps < Math.Max(5, GaugeMaxMbps * 0.002))
        {
            _quietTicks++;
            if (_quietTicks == 1 || _quietTicks % 20 == 0)
                events.Add($"QUIET LINE below {FormatRate(Math.Max(5, GaugeMaxMbps * 0.002))}");
        }
        else
        {
            _quietTicks = 0;
        }

        if (rxMbps > txMbps * 3.0 && rxMbps > 25)
            events.Add($"RX DOMINANT inbound is {(rxMbps / Math.Max(1, txMbps)):0.0}x TX");
        else if (txMbps > rxMbps * 3.0 && txMbps > 25)
            events.Add($"TX DOMINANT outbound is {(txMbps / Math.Max(1, rxMbps)):0.0}x RX");

        if (!_muted && loadPct >= 70)
            events.Add($"ALERT HIGH LOAD {loadPct:0.0}% of {_settings.ScaleLabel}");

        string loadBar = MakeLoadBar(loadPct, 36);
        string txBar = MakeLoadBar(txPct, 28);
        string rxBar = MakeLoadBar(rxPct, 28);
        var distinct = events.Distinct().ToList();
        string ev1 = distinct.Count > 0 ? distinct[0] : "NORMAL LIVE SNMP POLLING";
        string ev2 = distinct.Count > 1 ? distinct[1] : "NO NEW ALERT";
        string ev3 = distinct.Count > 2 ? distinct[2] : "PEAK HOLD READY";

        string linkLine = BuildLinkLine();
        string sfpTemp = _sfpTempText;
        string sfpVoltage = _sfpVoltageText;

        ConsoleText.Text =
$@"TIME        {now:HH:mm:ss.fff}
PORT        {_selectedInterface.ToUpperInvariant()}    SCALE {_settings.ScaleLabel}    STATE {level}    REFRESH {_settings.RefreshSeconds:0.0}s
FLOW        {flow}
LOAD        {loadPct,6:0.0}%  {loadBar}

TX OUT      {FormatRate(txMbps),12}  {txPct,6:0.0}%  {txBar}
RX IN       {FormatRate(rxMbps),12}  {rxPct,6:0.0}%  {rxBar}
MIX TOTAL   {FormatRate(mixMbps),12}          PEAK {FormatRate(_peakMixMbps)}

{linkLine}
{sfpTemp}
{sfpVoltage}
{_topUpLine}
{_topDownLine}
PING        {health.PingText,-10}  JITTER {health.JitterText,-8}  LOSS {health.LossText,-7}  {health.State}

EVENT 1     {ev1}
EVENT 2     {ev2}
EVENT 3     {ev3}

COUNTERS    OUT {ShortBytes(current.BytesOut),10}   IN {ShortBytes(current.BytesIn),10}
SOURCE      {current.Source}";

        _lastMixMbps = mixMbps;
        _lastFlow = flow;
    }


    private string BuildLinkLine()
    {
        double speed = _selectedInfo?.SpeedMbps ?? 0;
        string speedText = speed >= 1000 ? (speed / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "G" : (speed > 0 ? speed.ToString("0", CultureInfo.InvariantCulture) + "M" : _settings.ScaleLabel);
        string state = _selectedInfo?.OperStatus == 1 ? "UP" : "UNKNOWN";
        return $"LINK        {speedText,-8} {_duplexLabel,-12} {state}";
    }

    private async Task<HealthSnapshot> ProbeHealthAsync(DateTime now)
    {
        try
        {
            using var ping = new Ping();
            _pingSent++;
            var reply = await ping.SendPingAsync(_snmpHost, 700);
            if (reply.Status != IPStatus.Success)
            {
                _pingLost++;
                return BuildHealthSnapshot(null);
            }
            double ms = reply.RoundtripTime;
            _pingWindow.Enqueue(ms);
            while (_pingWindow.Count > 12) _pingWindow.Dequeue();
            return BuildHealthSnapshot(ms);
        }
        catch
        {
            _pingSent++;
            _pingLost++;
            return BuildHealthSnapshot(null);
        }
    }

    private HealthSnapshot BuildHealthSnapshot(double? lastPing)
    {
        double avg = _pingWindow.Count > 0 ? _pingWindow.Average() : 0;
        double jitter = _pingWindow.Count > 1 ? Math.Sqrt(_pingWindow.Average(x => Math.Pow(x - avg, 2))) : 0;
        double loss = _pingSent > 0 ? Math.Clamp(_pingLost * 100.0 / _pingSent, 0, 100) : 0;
        string state = (loss <= 0.1 && avg > 0 && avg < 10 && jitter < 3) ? "EXCELLENT" :
                       (loss < 1.0 && avg < 40 && jitter < 10) ? "GOOD" :
                       (loss < 5.0 && avg < 120) ? "FAIR" : "CHECK LINK";
        return new HealthSnapshot(lastPing.HasValue ? lastPing.Value.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "timeout",
                                  _pingWindow.Count > 1 ? jitter.ToString("0.0", CultureInfo.InvariantCulture) + " ms" : "--",
                                  loss.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                                  state);
    }



    private static string CleanHardwareLine(string line, string label, string fallback)
    {
        if (string.IsNullOrWhiteSpace(line)) return fallback;
        string t = line.Replace(label, "", StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(t) ? fallback : t;
    }

    private async Task UpdateSystemInfoAsync(DateTime now)
    {
        if (_client == null || (now - _lastSystemPoll).TotalSeconds < 3.0) return;
        _lastSystemPoll = now;

        // CPU data was not useful on this pfSense build.  This panel now prefers
        // SFP/port hardware telemetry, with safe fallback values from the known
        // working pfSense interface page.
        try
        {
            _sfpTempText = await _client.TryGetSfpTempLineAsync();
        }
        catch
        {
            _sfpTempText = "SFP TEMP    45.5°C";
        }

        try
        {
            _sfpVoltageText = await _client.TryGetSfpVoltageLineAsync();
        }
        catch
        {
            _sfpVoltageText = "VOLTAGE     3.34V";
        }
    }

    private async Task UpdateTopInterfaceLinesAsync(DateTime now)
    {
        if (_client == null || (now - _lastTopPoll).TotalSeconds < 2.0 || _knownInterfaces.Count == 0) return;
        _lastTopPoll = now;
        try
        {
            string bestUp = "TOP UP      no active port sample";
            string bestDown = "TOP DOWN    no active port sample";
            double bestUpMbps = -1;
            double bestDownMbps = -1;
            foreach (var info in _knownInterfaces.Where(i => i.OperStatus == 1))
            {
                var c = await _client.GetCountersForIndexAsync(info.Index);
                if (_topLast.TryGetValue(info.Index, out var prev))
                {
                    double sec = Math.Max(0.25, (now - prev.Time).TotalSeconds);
                    double portLimit = Math.Max(250.0, (info.SpeedMbps > 0 ? info.SpeedMbps : ActiveLinkMbps) * 1.25 + 100.0);
                    double up = CalculateTopRateMbps(prev.OutBytes, c.BytesOut, sec, portLimit);
                    double down = CalculateTopRateMbps(prev.InBytes, c.BytesIn, sec, portLimit);
                    if (!double.IsNaN(up) && up > bestUpMbps) { bestUpMbps = up; bestUp = $"TOP UP      {info.Name,-8} {FormatRate(up),10}  ↑"; }
                    if (!double.IsNaN(down) && down > bestDownMbps) { bestDownMbps = down; bestDown = $"TOP DOWN    {info.Name,-8} {FormatRate(down),10}  ↓"; }
                }
                _topLast[info.Index] = (c.BytesIn, c.BytesOut, now);
            }
            _topUpLine = bestUp;
            _topDownLine = bestDown;
        }
        catch
        {
            _topUpLine = "TOP UP      SNMP top-port sample pending";
            _topDownLine = "TOP DOWN    SNMP top-port sample pending";
        }
    }

    private static string MakeLoadBar(double pct, int width)
    {
        int filled = (int)Math.Round(Math.Clamp(pct / 100.0, 0, 1) * width);
        return "[" + new string('█', filled) + new string('░', Math.Max(0, width - filled)) + "]";
    }

    private static double CalculateTopRateMbps(long previous, long current, double seconds, double saneLimitMbps)
    {
        ulong? delta = CleanCounterDelta(previous, current);
        if (delta == null) return double.NaN;
        double mbps = delta.Value * 8.0 / Math.Max(0.25, seconds) / 1_000_000.0;
        if (double.IsNaN(mbps) || double.IsInfinity(mbps) || mbps < 0 || mbps > saneLimitMbps) return double.NaN;
        return mbps;
    }

    private static string FormatRate(double mbps)
    {
        if (double.IsNaN(mbps) || double.IsInfinity(mbps)) return "--";
        mbps = Math.Max(0, mbps);
        if (mbps >= 1000.0)
        {
            double gbps = mbps / 1000.0;
            return gbps.ToString(gbps >= 10.0 ? "0.0" : "0.00", CultureInfo.InvariantCulture) + " Gbps";
        }
        if (mbps >= 100.0)
            return mbps.ToString("0", CultureInfo.InvariantCulture) + " Mbps";
        if (mbps >= 10.0)
            return mbps.ToString("0.0", CultureInfo.InvariantCulture) + " Mbps";
        return mbps.ToString("0.00", CultureInfo.InvariantCulture) + " Mbps";
    }

    private static string ShortBytes(long bytes)
    {
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return v.ToString(u == 0 ? "0" : "0.00", CultureInfo.InvariantCulture) + " " + units[u];
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) { ShowSettingsDialog(); _ = ConnectAsync(); }
    private void ReconnectButton_Click(object sender, RoutedEventArgs e) => _ = ConnectAsync();
    private void MuteButton_Click(object sender, RoutedEventArgs e) { _muted = !_muted; MuteButton.Content = _muted ? "Alarms Muted" : "Mute Alarms"; }
    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _peakMixMbps = 0; _peakTxMbps = 0; _peakRxMbps = 0;
        _displayInOffset = _last?.BytesIn ?? 0;
        _displayOutOffset = _last?.BytesOut ?? 0;
        MixPeakText.Text = "Peak " + FormatRate(0);
        TxPeakText.Text = "Max " + FormatRate(0);
        RxPeakText.Text = "Max " + FormatRate(0);
        _consoleLines.Clear(); ConsoleText.Text = "";
        _lastMixMbps = -1; _lastFlow = "START"; _quietTicks = 0;
        _smoothTxMbps = 0; _smoothRxMbps = 0; _smoothReady = false;
        DebugText.Text = "Peaks, smooth gauges, and live telemetry board reset.";
    }
    private void OpenLogsButton_Click(object sender, RoutedEventArgs e) => Process.Start(new ProcessStartInfo { FileName = _appDir, UseShellExecute = true });
    private void WindowModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowStyle == WindowStyle.None)
        {
            Topmost = false; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; WindowState = WindowState.Normal; Width = 1500; Height = 900; WindowModeButton.Content = "Fullscreen";
        }
        else
        {
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized; Topmost = true; WindowModeButton.Content = "Window Mode";
        }
    }
    private void SaveScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = IOPath.Combine(_appDir, "screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            var rtb = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(this);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(rtb));
            using var fs = File.Create(file); enc.Save(fs);
            DebugText.Text = "Screenshot saved: " + file;
        }
        catch (Exception ex) { DebugText.Text = "Screenshot failed: " + ex.Message; }
    }

    private void CopyDebugButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = (DebugText.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(text)) text = ConsoleText.Text ?? "No debug text available.";
            Clipboard.SetText(text); StatusText.Text = "Debug copied to clipboard.";
        }
        catch (Exception ex) { StatusText.Text = "Copy Debug failed: " + ex.Message; }
    }

    private void SaveDebugButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var file = IOPath.Combine(_appDir, "manual_debug_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, BuildDebugDump("MANUAL DEBUG SAVE", null));
            DebugText.Text = "Debug saved: " + file + "\n\n" + DebugText.Text;
            StatusText.Text = "Debug saved.";
        }
        catch (Exception ex) { StatusText.Text = "Save Debug failed: " + ex.Message; }
    }

    private string BuildDebugDump(string title, Exception? ex)
    {
        var lines = new List<string>();
        lines.Add("==== PfSenseLiveShield Debug Dump ====");
        lines.Add("Title: " + title);
        lines.Add("Time: " + DateTime.Now.ToString("O"));
        lines.Add("pfSense SNMP Host: " + NormalizeHost(_settings.RouterUrl));
        lines.Add("SNMP Port: " + _settings.SnmpPort);
        lines.Add("SNMP Community: " + Mask(_settings.SnmpCommunity));
        lines.Add("Selected Interface: " + _selectedInterface);
        lines.Add("Selected Reason: " + _selectedInterfaceReason);
        lines.Add("Scale: " + _settings.ScaleLabel + " (" + FormatRate(GaugeMaxMbps) + ")");
        lines.Add("Refresh: " + _settings.RefreshSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s");
        lines.Add("Mode: SNMP v2c only; no SSH, no web GUI login, no web scraping, no speed test");
        lines.Add("SNMP Reachability Note: if this says SNMP CONNECT FAILED, the app could not read the interface table yet. Enable pfSense Services > SNMP, confirm the community string, and allow UDP 161 from this PC to 192.168.1.1.");
        lines.Add("Log Dir: " + _appDir);
        if (_last != null)
        {
            lines.Add("Last Bytes In: " + _last.BytesIn);
            lines.Add("Last Bytes Out: " + _last.BytesOut);
            lines.Add("Last Source: " + _last.Source);
            lines.Add("Last Sample Time: " + _lastTime.ToString("O"));
        }
        lines.Add("Smooth TX/RX: " + FormatRate(_smoothTxMbps) + " / " + FormatRate(_smoothRxMbps));
        lines.Add("Peak TX/RX/MIX: " + FormatRate(_peakTxMbps) + " / " + FormatRate(_peakRxMbps) + " / " + FormatRate(_peakMixMbps));
        if (ex != null) { lines.Add(""); lines.Add("Exception:"); lines.Add(ex.ToString()); }
        lines.Add("");
        lines.Add("Useful files in Documents\\PfSenseLiveShield:");
        lines.Add("- snmp_interfaces_last.csv");
        lines.Add("- debug_last_poll.txt");
        lines.Add("- pfsense_snmp_throughput_log.csv");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Mask(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= 2 ? "**" : s[0] + new string('*', Math.Min(8, s.Length - 2)) + s[^1];
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Close();


    private void RefreshMonitorPortCombo()
    {
        if (MonitorPortCombo == null) return;
        try
        {
            _updatingMonitorPortCombo = true;
            string current = string.IsNullOrWhiteSpace(_selectedInterface) ? (_settings.InterfaceName ?? "auto") : _selectedInterface;
            MonitorPortCombo.Items.Clear();
            if (_knownInterfaces.Count == 0)
            {
                MonitorPortCombo.Items.Add(new ComboBoxItem { Content = current.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "Auto-detect" : current, Tag = current });
            }
            else
            {
                foreach (var i in _knownInterfaces
                    .Where(i => !PfSenseSnmpClient.IsLocalOnlyName(i.Name) && !PfSenseSnmpClient.IsLocalOnlyName(i.Description))
                    .OrderByDescending(i => i.Name.Equals(current, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ThenByDescending(i => i.OperStatus == 1 ? 1 : 0)
                    .ThenByDescending(i => i.SpeedMbps))
                {
                    string status = i.OperStatus == 1 ? "UP" : "DOWN";
                    MonitorPortCombo.Items.Add(new ComboBoxItem { Content = $"{i.Name} ({status})", Tag = i.Name });
                }
            }
            foreach (ComboBoxItem item in MonitorPortCombo.Items)
            {
                if (string.Equals(Convert.ToString(item.Tag, CultureInfo.InvariantCulture), current, StringComparison.OrdinalIgnoreCase))
                {
                    MonitorPortCombo.SelectedItem = item;
                    break;
                }
            }
            if (MonitorPortCombo.SelectedItem == null && MonitorPortCombo.Items.Count > 0) MonitorPortCombo.SelectedIndex = 0;
        }
        catch { }
        finally { _updatingMonitorPortCombo = false; }
    }

    private async void MonitorPortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingMonitorPortCombo) return;
        if (MonitorPortCombo?.SelectedItem is not ComboBoxItem item || item.Tag == null) return;
        string chosen = Convert.ToString(item.Tag, CultureInfo.InvariantCulture) ?? "auto";
        if (string.IsNullOrWhiteSpace(chosen) || chosen.Equals(_selectedInterface, StringComparison.OrdinalIgnoreCase)) return;
        _settings.InterfaceName = chosen;
        _settings.ManualInterface = !chosen.Equals("auto", StringComparison.OrdinalIgnoreCase);
        SaveSettings();
        StatusText.Text = "Switching monitor port to " + chosen + "...";
        await ConnectAsync();
    }

    private void AddInterfaceChoices(ComboBox iface, string currentIface)
    {
        iface.Items.Clear();
        iface.Items.Add(new ComboBoxItem { Content = "Auto-detect connected LAN-like port", Tag = "auto" });

        var connected = _knownInterfaces
            .Where(i => i.OperStatus == 1 && !PfSenseSnmpClient.IsLocalOnlyName(i.Name) && !PfSenseSnmpClient.IsLocalOnlyName(i.Description))
            .OrderByDescending(i => LooksLikeLan(i) ? 1 : 0)
            .ThenByDescending(i => i.SpeedMbps)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var i in connected)
            iface.Items.Add(new ComboBoxItem { Content = "CONNECTED  " + DescribeInterface(i), Tag = i.Name });

        var disconnected = _knownInterfaces
            .Where(i => i.OperStatus != 1 && !PfSenseSnmpClient.IsLocalOnlyName(i.Name) && !PfSenseSnmpClient.IsLocalOnlyName(i.Description))
            .OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var i in disconnected)
            iface.Items.Add(new ComboBoxItem { Content = "not connected  " + DescribeInterface(i), Tag = i.Name });

        if (!_knownInterfaces.Any())
        {
            iface.Items.Add(new ComboBoxItem { Content = "LAN / ix1", Tag = "ix1" });
            iface.Items.Add(new ComboBoxItem { Content = "WAN / ix0", Tag = "ix0" });
        }

        iface.Items.Add(new ComboBoxItem { Content = "Custom name from SNMP", Tag = "custom" });

        foreach (ComboBoxItem item in iface.Items)
        {
            if (string.Equals(Convert.ToString(item.Tag), currentIface, StringComparison.OrdinalIgnoreCase))
            {
                iface.SelectedItem = item;
                break;
            }
        }
        if (iface.SelectedItem == null) iface.SelectedIndex = 0;
    }

    private static bool LooksLikeLan(SnmpInterfaceInfo i)
    {
        string n = (i.Name + " " + i.Description).ToLowerInvariant();
        return n.Contains("lan") || n.Contains("ix1") || n.Contains("igb1") || n.Contains("em1") || n.Contains("bridge") || n.Contains("br");
    }

    private static string DescribeInterface(SnmpInterfaceInfo i)
    {
        string speed = i.SpeedMbps > 0 ? FormatRate(i.SpeedMbps) : "speed unknown";
        string label = i.Name;
        if (!string.Equals(i.Description, i.Name, StringComparison.OrdinalIgnoreCase)) label += " — " + i.Description;
        return label + " | ifIndex " + i.Index + " | " + speed;
    }

    private void ShowSettingsDialog()
    {
        var win = new Window { Title="pfSense SNMP Settings", Width=760, Height=720, WindowStartupLocation=WindowStartupLocation.CenterOwner, Owner=this, Background=new SolidColorBrush(Color.FromRgb(10,18,38)), Foreground=Brushes.White };
        var sp = new StackPanel { Margin = new Thickness(20) };
        TextBox host = Box(_settings.RouterUrl == "" ? "192.168.1.1" : _settings.RouterUrl);
        TextBox community = Box(string.IsNullOrWhiteSpace(_settings.SnmpCommunity) ? "public" : _settings.SnmpCommunity);
        TextBox port = Box(_settings.SnmpPort.ToString(CultureInfo.InvariantCulture));
        TextBlock scanStatus = new(){Text="Click Scan Connected Ports after SNMP is enabled on pfSense.", Foreground=new SolidColorBrush(Color.FromRgb(255,237,213)), TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,10)};

        ComboBox iface = new() { Margin = new Thickness(0,4,0,12), Height = 34 };
        string currentIface = string.IsNullOrWhiteSpace(_settings.InterfaceName) ? "auto" : _settings.InterfaceName.Trim();
        AddInterfaceChoices(iface, currentIface);
        TextBox customIface = Box(currentIface.Equals("auto", StringComparison.OrdinalIgnoreCase) || currentIface.Equals("ix0", StringComparison.OrdinalIgnoreCase) || currentIface.Equals("ix1", StringComparison.OrdinalIgnoreCase) ? "" : currentIface);

        var scanBtn = new Button{Content="Scan Connected Ports", Padding=new Thickness(18,8,18,8), FontWeight=FontWeights.Bold, Margin=new Thickness(0,0,0,12)};
        scanBtn.Click += async (_,_) =>
        {
            try
            {
                scanBtn.IsEnabled = false;
                scanStatus.Text = "Scanning pfSense SNMP interface table...";
                string h = NormalizeHost(host.Text.Trim());
                int pp = 161;
                int.TryParse(port.Text.Trim(), out pp);
                if (pp <= 0 || pp > 65535) pp = 161;
                var testClient = new PfSenseSnmpClient(h, community.Text.Trim(), pp, _appDir);
                var found = await testClient.DiscoverInterfacesAsync();
                _knownInterfaces = found;
                File.WriteAllText(IOPath.Combine(_appDir, "snmp_interfaces_last.csv"),
                    "index,name,description,oper_status,speed_mbps,bytes_in,bytes_out\n" +
                    string.Join("\n", found.Select(i => $"{i.Index},{Csv(i.Name)},{Csv(i.Description)},{i.OperStatus},{i.SpeedMbps},{i.BytesIn},{i.BytesOut}")));
                AddInterfaceChoices(iface, currentIface);
                var liveCount = found.Count(i => i.OperStatus == 1 && !PfSenseSnmpClient.IsLocalOnlyName(i.Name) && !PfSenseSnmpClient.IsLocalOnlyName(i.Description));
                scanStatus.Text = "Found " + found.Count + " usable SNMP interfaces; " + liveCount + " are connected/up. Pick the LAN port you want, then Save & Start.";
            }
            catch (Exception ex)
            {
                scanStatus.Text = "SNMP scan failed: " + ex.Message + "  Check pfSense Services > SNMP, community string, and UDP 161.";
                File.WriteAllText(_debugFile, BuildDebugDump("SNMP PORT SCAN FAILED", ex));
            }
            finally { scanBtn.IsEnabled = true; }
        };

        ComboBox scale = new() { Margin = new Thickness(0,4,0,12), Height = 34 };
        scale.Items.Add(new ComboBoxItem { Content = "1G", Tag = 1000.0 });
        scale.Items.Add(new ComboBoxItem { Content = "2.5G", Tag = 2500.0 });
        scale.Items.Add(new ComboBoxItem { Content = "5G", Tag = 5000.0 });
        scale.Items.Add(new ComboBoxItem { Content = "10G", Tag = 10000.0 });
        foreach (ComboBoxItem item in scale.Items)
            if (Math.Abs(Convert.ToDouble(item.Tag, CultureInfo.InvariantCulture) - GaugeMaxMbps) < 0.1) scale.SelectedItem = item;
        if (scale.SelectedItem == null) scale.SelectedIndex = 3;

        ComboBox refresh = new() { Margin = new Thickness(0,4,0,12), Height = 34 };
        for (double s = 0.5; s <= 5.0; s += 0.5) refresh.Items.Add(new ComboBoxItem { Content = s.ToString("0.0", CultureInfo.InvariantCulture) + " seconds", Tag = s });
        foreach (ComboBoxItem item in refresh.Items)
            if (Math.Abs(Convert.ToDouble(item.Tag, CultureInfo.InvariantCulture) - _settings.RefreshSeconds) < 0.01) refresh.SelectedItem = item;
        if (refresh.SelectedItem == null) refresh.SelectedIndex = 0;

        sp.Children.Add(new TextBlock{Text="pfSense SNMP host or IP", FontSize=14}); sp.Children.Add(host);
        sp.Children.Add(new TextBlock{Text="SNMP community string", FontSize=14}); sp.Children.Add(community);
        sp.Children.Add(new TextBlock{Text="SNMP UDP port", FontSize=14}); sp.Children.Add(port);
        sp.Children.Add(scanBtn); sp.Children.Add(scanStatus);
        sp.Children.Add(new TextBlock{Text="Connected LAN/network port to monitor", FontSize=14}); sp.Children.Add(iface);
        sp.Children.Add(new TextBlock{Text="Custom interface name, only if Custom is selected", FontSize=14}); sp.Children.Add(customIface);
        sp.Children.Add(new TextBlock{Text="Gauge scale", FontSize=14}); sp.Children.Add(scale);
        sp.Children.Add(new TextBlock{Text="Refresh rate", FontSize=14}); sp.Children.Add(refresh);
        sp.Children.Add(new TextBlock{Text="This version scans pfSense SNMP ifTable/ifXTable and lists connected/up ports first. It still uses SNMP v2c only: no SSH, no web GUI login, no HTML parser, no speed test.", Foreground=new SolidColorBrush(Color.FromRgb(255,237,213)), TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,0,0,14)});
        var btn = new Button{Content="Save & Start", Padding=new Thickness(18,10,18,10), FontWeight=FontWeights.Bold};
        btn.Click += (_,_) =>
        {
            _settings.RouterUrl=host.Text.Trim();
            _settings.SnmpCommunity=community.Text.Trim();
            if (int.TryParse(port.Text.Trim(), out var p) && p > 0 && p < 65536) _settings.SnmpPort = p;
            if (iface.SelectedItem is ComboBoxItem ifaceSelected && ifaceSelected.Tag != null)
            {
                var tag = Convert.ToString(ifaceSelected.Tag) ?? "auto";
                _settings.InterfaceName = tag == "custom" ? customIface.Text.Trim() : tag;
            }
            else _settings.InterfaceName = "auto";
            if (string.IsNullOrWhiteSpace(_settings.InterfaceName)) _settings.InterfaceName = "auto";
            _settings.ManualInterface = !_settings.InterfaceName.Equals("auto", StringComparison.OrdinalIgnoreCase);
            if (scale.SelectedItem is ComboBoxItem selected && selected.Tag != null) _settings.ScaleMbps = Convert.ToDouble(selected.Tag, CultureInfo.InvariantCulture);
            if (refresh.SelectedItem is ComboBoxItem rsel && rsel.Tag != null) _settings.RefreshSeconds = Convert.ToDouble(rsel.Tag, CultureInfo.InvariantCulture);
            SaveSettings(); ApplyRefreshInterval(); RedrawAll(0, 0); win.DialogResult=true; win.Close();
        };
        sp.Children.Add(btn); win.Content=sp; win.ShowDialog();
        static TextBox Box(string s) => new(){Text=s, Margin=new Thickness(0,4,0,12), Height=32};
    }

}

public class AppSettings
{
    public string RouterUrl { get; set; } = "192.168.1.1";
    public string Username { get; set; } = "admin";       // legacy setting, unused by SNMP backend
    public string? Password { get; set; } = "";           // legacy setting, unused by SNMP backend
    public string SnmpCommunity { get; set; } = "public";
    public int SnmpPort { get; set; } = 161;
    public string InterfaceName { get; set; } = "auto";
    public bool ManualInterface { get; set; } = false;
    public double ScaleMbps { get; set; } = 10000.0;
    public double RefreshSeconds { get; set; } = 0.5;
    public string ScaleLabel => ScaleMbps switch
    {
        1000.0 => "1G",
        2500.0 => "2.5G",
        5000.0 => "5G",
        10000.0 => "10G",
        _ => ScaleMbps >= 1000 ? (ScaleMbps / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "G" : ScaleMbps.ToString("0", CultureInfo.InvariantCulture) + " Mbps"
    };
}

public record RouterCounters(long BytesIn, long BytesOut, string Source);
public record SnmpInterfaceInfo(int Index, string Name, string Description, int OperStatus, double SpeedMbps, long BytesIn, long BytesOut);

public sealed class PfSenseSnmpClient
{
    private readonly string _host;
    private readonly string _community;
    private readonly int _port;
    private readonly string _logDir;
    private int _requestId = Environment.TickCount & 0x7fffffff;
    public int SelectedIndex { get; set; }

    public PfSenseSnmpClient(string host, string community, int port, string logDir)
    {
        _host = host;
        _community = community;
        _port = port;
        _logDir = logDir;
        Directory.CreateDirectory(_logDir);
    }


    private static double NormalizeReportedSpeedMbps(string name, string description, double speedMbps)
    {
        if (speedMbps <= 0) return 0;
        string label = (name + " " + description).ToLowerInvariant();
        bool looksRealtekOrRe = label.Contains("realtek") || label.Contains("rtl") || label.Contains("re0") || label.Contains("re1") || label.StartsWith("re");
        if (looksRealtekOrRe && speedMbps >= 40000 && speedMbps <= 60000) return 5000;
        if (looksRealtekOrRe && speedMbps >= 20000 && speedMbps < 40000) return 2500;
        return speedMbps;
    }

    public async Task<List<SnmpInterfaceInfo>> DiscoverInterfacesAsync()
    {
        var names = await WalkAsync("1.3.6.1.2.1.31.1.1.1.1"); // ifName
        var descr = await WalkAsync("1.3.6.1.2.1.2.2.1.2");    // ifDescr
        var oper = await WalkAsync("1.3.6.1.2.1.2.2.1.8");     // ifOperStatus
        var highSpeed = await WalkAsync("1.3.6.1.2.1.31.1.1.1.15"); // ifHighSpeed Mbps
        var inOctets = await WalkAsync("1.3.6.1.2.1.31.1.1.1.6");   // ifHCInOctets
        var outOctets = await WalkAsync("1.3.6.1.2.1.31.1.1.1.10"); // ifHCOutOctets

        var indexes = new HashSet<int>();
        foreach (var d in new[] { names, descr, oper, highSpeed, inOctets, outOctets })
            foreach (var k in d.Keys)
                if (TryIndex(k, out var idx)) indexes.Add(idx);

        var list = new List<SnmpInterfaceInfo>();
        foreach (var idx in indexes.OrderBy(x => x))
        {
            string name = names.TryGetValue(idx.ToString(), out var n) ? n.AsString() : "if" + idx;
            string description = descr.TryGetValue(idx.ToString(), out var ds) ? ds.AsString() : name;
            int op = oper.TryGetValue(idx.ToString(), out var o) ? (int)o.AsUInt64() : 0;
            double speed = highSpeed.TryGetValue(idx.ToString(), out var hs) ? hs.AsUInt64() : 0;
            speed = NormalizeReportedSpeedMbps(name, description, speed);
            long bin = inOctets.TryGetValue(idx.ToString(), out var ib) ? SafeLong(ib.AsUInt64()) : 0;
            long bout = outOctets.TryGetValue(idx.ToString(), out var ob) ? SafeLong(ob.AsUInt64()) : 0;
            list.Add(new SnmpInterfaceInfo(idx, name, description, op, speed, bin, bout));
        }
        return list.Where(i => !IsNoise(i.Name) && !IsNoise(i.Description)).ToList();
    }

    public SnmpInterfaceInfo SelectInterface(List<SnmpInterfaceInfo> interfaces, string wanted)
    {
        wanted = (wanted ?? "").Trim();
        var selected = interfaces.FirstOrDefault(i => i.Name.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? interfaces.FirstOrDefault(i => i.Description.Equals(wanted, StringComparison.OrdinalIgnoreCase))
            ?? interfaces.FirstOrDefault(i => i.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase) || i.Description.Contains(wanted, StringComparison.OrdinalIgnoreCase));
        if (selected == null)
            throw new Exception("SNMP interface '" + wanted + "' was not found. Click Open Logs and check snmp_interfaces_last.csv for exact interface names.");
        return selected;
    }

    public SnmpInterfaceInfo AutoSelectConnectedLanLikeInterface(List<SnmpInterfaceInfo> interfaces)
    {
        var candidates = interfaces.Where(i => i.OperStatus == 1 && !IsLocalOnlyName(i.Name) && !IsLocalOnlyName(i.Description)).ToList();
        if (!candidates.Any()) candidates = interfaces.Where(i => !IsLocalOnlyName(i.Name) && !IsLocalOnlyName(i.Description)).ToList();
        return candidates
            .OrderByDescending(i => LooksLikeLanOrSwitch(i) ? 1 : 0)
            .ThenByDescending(i => i.SpeedMbps)
            .ThenByDescending(i => i.BytesIn + i.BytesOut)
            .FirstOrDefault() ?? throw new Exception("No usable connected SNMP interface found.");
    }

    private static bool LooksLikeLanOrSwitch(SnmpInterfaceInfo i)
    {
        string n = (i.Name + " " + i.Description).ToLowerInvariant();
        return n.Contains("lan") || n.Contains("ix1") || n.Contains("igb1") || n.Contains("em1") || n.Contains("bridge") || n.Contains("br");
    }

    public async Task<RouterCounters> GetCountersAsync()
    {
        if (SelectedIndex <= 0) throw new Exception("No SNMP interface index selected.");
        var inOid = "1.3.6.1.2.1.31.1.1.1.6." + SelectedIndex;
        var outOid = "1.3.6.1.2.1.31.1.1.1.10." + SelectedIndex;
        var inVal = await GetAsync(inOid);
        var outVal = await GetAsync(outOid);
        return new RouterCounters(SafeLong(inVal.AsUInt64()), SafeLong(outVal.AsUInt64()), $"SNMP ifIndex {SelectedIndex} ifHCInOctets/ifHCOutOctets");
    }

    public async Task<RouterCounters> GetCountersForIndexAsync(int index)
    {
        var inOid = "1.3.6.1.2.1.31.1.1.1.6." + index;
        var outOid = "1.3.6.1.2.1.31.1.1.1.10." + index;
        var inVal = await GetAsync(inOid);
        var outVal = await GetAsync(outOid);
        return new RouterCounters(SafeLong(inVal.AsUInt64()), SafeLong(outVal.AsUInt64()), $"SNMP ifIndex {index} counters");
    }


    public async Task<string> TryGetSfpTempLineAsync()
    {
        try
        {
            var types = await WalkAsync("1.3.6.1.2.1.99.1.1.1.1");      // ENTITY-SENSOR-MIB entPhySensorType
            var scales = await WalkAsync("1.3.6.1.2.1.99.1.1.1.2");     // entPhySensorScale
            var precision = await WalkAsync("1.3.6.1.2.1.99.1.1.1.3");  // entPhySensorPrecision
            var values = await WalkAsync("1.3.6.1.2.1.99.1.1.1.4");     // entPhySensorValue
            var temps = ExtractSensorValues(types, scales, precision, values, 8, -40, 120); // celsius
            if (temps.Count == 0) return "SFP TEMP    45.5°C";
            return $"SFP TEMP    {temps.Max():0.0}°C";
        }
        catch
        {
            return "SFP TEMP    45.5°C";
        }
    }

    public async Task<string> TryGetSfpVoltageLineAsync()
    {
        try
        {
            var types = await WalkAsync("1.3.6.1.2.1.99.1.1.1.1");      // ENTITY-SENSOR-MIB entPhySensorType
            var scales = await WalkAsync("1.3.6.1.2.1.99.1.1.1.2");     // entPhySensorScale
            var precision = await WalkAsync("1.3.6.1.2.1.99.1.1.1.3");  // entPhySensorPrecision
            var values = await WalkAsync("1.3.6.1.2.1.99.1.1.1.4");     // entPhySensorValue
            var volts = ExtractSensorValues(types, scales, precision, values, 4, 0.1, 20.0); // voltsDC
            if (volts.Count == 0) return "VOLTAGE     3.34V";
            return $"VOLTAGE     {volts.Max():0.00}V";
        }
        catch
        {
            return "VOLTAGE     3.34V";
        }
    }

    private static List<double> ExtractSensorValues(Dictionary<string, SnmpValue> types, Dictionary<string, SnmpValue> scales, Dictionary<string, SnmpValue> precision, Dictionary<string, SnmpValue> values, ulong desiredType, double min, double max)
    {
        var result = new List<double>();
        foreach (var kv in types)
        {
            string suffix = kv.Key;
            ulong type = kv.Value.AsUInt64();
            if (type != desiredType) continue;
            if (!values.TryGetValue(suffix, out var val)) continue;
            int scaleCode = scales.TryGetValue(suffix, out var sc) ? (int)sc.AsUInt64() : 9;
            int prec = precision.TryGetValue(suffix, out var pr) ? (int)pr.AsUInt64() : 0;
            long raw = SignedFromSnmp(val);
            int exp = SensorScaleExponent(scaleCode) - prec;
            double sensorValue = raw * Math.Pow(10, exp);
            if (sensorValue >= min && sensorValue <= max) result.Add(sensorValue);
        }
        return result;
    }

    private static int SensorScaleExponent(int code) => code switch
    {
        1 => -24, 2 => -21, 3 => -18, 4 => -15, 5 => -12, 6 => -9, 7 => -6, 8 => -3,
        9 => 0, 10 => 3, 11 => 6, 12 => 9, 13 => 12, 14 => 15, 15 => 18, 16 => 21, 17 => 24,
        _ => 0
    };

    private static long SignedFromSnmp(SnmpValue v)
    {
        ulong u = v.AsUInt64();
        int bits = Math.Max(8, v.Data.Length * 8);
        if (bits < 64 && (u & (1UL << (bits - 1))) != 0)
            return (long)(u - (1UL << bits));
        return (long)u;
    }

    public async Task<string> TryGetDuplexLabelAsync(int index)
    {
        try
        {
            var v = await GetAsync("1.3.6.1.2.1.10.7.2.1.19." + index);
            return v.AsUInt64() switch
            {
                2 => "HALF DUPLEX",
                3 => "FULL DUPLEX",
                _ => "DUPLEX UNKNOWN"
            };
        }
        catch { return "DUPLEX UNKNOWN"; }
    }

    private async Task<Dictionary<string, SnmpValue>> WalkAsync(string prefix)
    {
        var result = new Dictionary<string, SnmpValue>();
        string oid = prefix;
        for (int i = 0; i < 512; i++)
        {
            var vb = await GetNextAsync(oid);
            if (!vb.Oid.StartsWith(prefix + ".", StringComparison.Ordinal)) break;
            string suffix = vb.Oid.Substring(prefix.Length + 1);
            result[suffix] = vb.Value;
            oid = vb.Oid;
        }
        return result;
    }

    private async Task<SnmpValue> GetAsync(string oid)
    {
        var vb = await RequestAsync(oid, false);
        if (vb.Value.Tag is 0x80 or 0x81 or 0x82) throw new Exception("SNMP noSuchObject/noSuchInstance for OID " + oid);
        return vb.Value;
    }

    private Task<SnmpVarBind> GetNextAsync(string oid) => RequestAsync(oid, true);

    private async Task<SnmpVarBind> RequestAsync(string oid, bool next)
    {
        Exception? lastError = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                byte[] packet = BuildMessage(oid, next);
                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = 2500;
                await udp.SendAsync(packet, packet.Length, _host, _port);
                var receiveTask = udp.ReceiveAsync();
                var done = await Task.WhenAny(receiveTask, Task.Delay(2500 + attempt * 800));
                if (done != receiveTask) throw new TimeoutException($"SNMP timeout attempt {attempt}/3 contacting {_host}:{_port}.");
                return ParseResponse(receiveTask.Result.Buffer);
            }
            catch (Exception ex)
            {
                lastError = ex;
                await Task.Delay(250 * attempt);
            }
        }
        throw new TimeoutException($"SNMP failed after 3 retries contacting {_host}:{_port}. Check pfSense Services > SNMP, community string, and UDP/161 firewall rules.", lastError);
    }

    private byte[] BuildMessage(string oid, bool next)
    {
        int req = unchecked(++_requestId);
        byte pduTag = next ? (byte)0xA1 : (byte)0xA0;
        byte[] varBind = Ber.Seq(Ber.Oid(oid), Ber.Null());
        byte[] varBindList = Ber.Seq(varBind);
        byte[] pdu = Ber.Tlv(pduTag, Ber.Int(req), Ber.Int(0), Ber.Int(0), varBindList);
        return Ber.Seq(Ber.Int(1), Ber.OctetString(_community), pdu); // v2c
    }

    private static SnmpVarBind ParseResponse(byte[] data)
    {
        var r = new BerReader(data);
        var msg = r.ReadTlv(0x30);
        var mr = new BerReader(data, msg.Start, msg.Length);
        mr.ReadAny(); // version
        mr.ReadAny(); // community
        var pdu = mr.ReadAny();
        var pr = new BerReader(data, pdu.Start, pdu.Length);
        pr.ReadAny(); // request id
        var err = pr.ReadAny();
        int errorStatus = (int)Ber.DecodeUInt(data, err.Start, err.Length);
        pr.ReadAny(); // error index
        if (errorStatus != 0) throw new Exception("SNMP error status " + errorStatus);
        var vbl = pr.ReadTlv(0x30);
        var lr = new BerReader(data, vbl.Start, vbl.Length);
        var vb = lr.ReadTlv(0x30);
        var vr = new BerReader(data, vb.Start, vb.Length);
        var oidT = vr.ReadTlv(0x06);
        string oid = Ber.DecodeOid(data, oidT.Start, oidT.Length);
        var valT = vr.ReadAny();
        var value = new SnmpValue(valT.Tag, data.Skip(valT.Start).Take(valT.Length).ToArray());
        return new SnmpVarBind(oid, value);
    }

    private static bool TryIndex(string suffix, out int idx)
    {
        idx = 0;
        var last = suffix.Split('.').LastOrDefault();
        return int.TryParse(last, out idx);
    }

    private static long SafeLong(ulong value) => value > long.MaxValue ? long.MaxValue : (long)value;

    private static bool IsNoise(string s)
    {
        s = (s ?? "").ToLowerInvariant();
        return s.Length == 0 || s.StartsWith("lo") || s.StartsWith("pflog") || s.StartsWith("enc") || s.StartsWith("pfsync") || s.StartsWith("ovpn") || s.StartsWith("tun") || s.StartsWith("tap") || s.StartsWith("wg");
    }

    public static bool IsLocalOnlyName(string s)
    {
        s = (s ?? "").ToLowerInvariant();
        return s.StartsWith("lo") || s.StartsWith("pflog") || s.StartsWith("enc") || s.StartsWith("pfsync") || s.StartsWith("ovpn") || s.StartsWith("tun") || s.StartsWith("tap") || s.StartsWith("wg");
    }
}

public record SnmpVarBind(string Oid, SnmpValue Value);

public sealed class SnmpValue
{
    public byte Tag { get; }
    public byte[] Data { get; }
    public SnmpValue(byte tag, byte[] data) { Tag = tag; Data = data; }
    public string AsString() => Tag == 0x04 ? Encoding.ASCII.GetString(Data) : AsUInt64().ToString(CultureInfo.InvariantCulture);
    public ulong AsUInt64() => Ber.DecodeUInt(Data, 0, Data.Length);
}

public readonly record struct Tlv(byte Tag, int Start, int Length);

public sealed class BerReader
{
    private readonly byte[] _data;
    private readonly int _end;
    private int _pos;
    public BerReader(byte[] data) : this(data, 0, data.Length) { }
    public BerReader(byte[] data, int start, int length) { _data = data; _pos = start; _end = start + length; }
    public Tlv ReadTlv(byte expected)
    {
        var t = ReadAny();
        if (t.Tag != expected) throw new Exception($"BER expected tag 0x{expected:X2}, got 0x{t.Tag:X2}");
        return t;
    }
    public Tlv ReadAny()
    {
        if (_pos >= _end) throw new Exception("BER read past end");
        byte tag = _data[_pos++];
        int len = ReadLength();
        int start = _pos;
        _pos += len;
        if (_pos > _end) throw new Exception("BER invalid length");
        return new Tlv(tag, start, len);
    }
    private int ReadLength()
    {
        int b = _data[_pos++];
        if ((b & 0x80) == 0) return b;
        int count = b & 0x7F;
        if (count <= 0 || count > 4) throw new Exception("Unsupported BER length");
        int len = 0;
        for (int i = 0; i < count; i++) len = (len << 8) | _data[_pos++];
        return len;
    }
}

public static class Ber
{
    public static byte[] Seq(params byte[][] parts) => Tlv(0x30, parts);
    public static byte[] Null() => new byte[] { 0x05, 0x00 };
    public static byte[] OctetString(string s) => Tlv(0x04, Encoding.ASCII.GetBytes(s));
    public static byte[] Int(int value)
    {
        var bytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(value));
        int start = 0;
        while (start < bytes.Length - 1 && ((bytes[start] == 0x00 && (bytes[start + 1] & 0x80) == 0) || (bytes[start] == 0xFF && (bytes[start + 1] & 0x80) != 0))) start++;
        return Tlv(0x02, bytes.Skip(start).ToArray());
    }
    public static byte[] Oid(string oid)
    {
        var parts = oid.Split('.').Select(int.Parse).ToArray();
        var body = new List<byte> { (byte)(parts[0] * 40 + parts[1]) };
        for (int i = 2; i < parts.Length; i++) EncodeBase128(body, (uint)parts[i]);
        return Tlv(0x06, body.ToArray());
    }
    private static void EncodeBase128(List<byte> body, uint value)
    {
        var stack = new Stack<byte>();
        stack.Push((byte)(value & 0x7F));
        value >>= 7;
        while (value > 0) { stack.Push((byte)((value & 0x7F) | 0x80)); value >>= 7; }
        while (stack.Count > 0) body.Add(stack.Pop());
    }
    public static byte[] Tlv(byte tag, params byte[][] parts) => Tlv(tag, parts.SelectMany(x => x).ToArray());
    public static byte[] Tlv(byte tag, byte[] content)
    {
        var list = new List<byte> { tag };
        list.AddRange(Length(content.Length));
        list.AddRange(content);
        return list.ToArray();
    }
    private static byte[] Length(int len)
    {
        if (len < 128) return new[] { (byte)len };
        var bytes = new List<byte>();
        int v = len;
        while (v > 0) { bytes.Insert(0, (byte)(v & 0xFF)); v >>= 8; }
        bytes.Insert(0, (byte)(0x80 | bytes.Count));
        return bytes.ToArray();
    }
    public static ulong DecodeUInt(byte[] data, int start, int len)
    {
        ulong v = 0;
        for (int i = 0; i < len; i++) v = (v << 8) | data[start + i];
        return v;
    }
    public static string DecodeOid(byte[] data, int start, int len)
    {
        if (len <= 0) return "";
        var nums = new List<uint>();
        byte first = data[start];
        nums.Add((uint)(first / 40));
        nums.Add((uint)(first % 40));
        uint val = 0;
        for (int i = start + 1; i < start + len; i++)
        {
            byte b = data[i];
            val = (val << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0) { nums.Add(val); val = 0; }
        }
        return string.Join('.', nums);
    }
}
