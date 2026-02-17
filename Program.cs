using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using System.Globalization;

namespace ISPing
{
    public class ISPing : ApplicationContext
    {
        [DllImport("user32.dll", SetLastError = true)] private static extern bool AddClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
        [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)] extern static bool DestroyIcon(IntPtr handle);
        private const int WM_CLIPBOARDUPDATE = 0x031D;
        private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer pingTimer;
        private System.Windows.Forms.Timer tracertTimer;
        private System.Windows.Forms.Timer networkSpeedTimer;
        private System.Windows.Forms.Timer _ipCheckTimer;
        private System.Windows.Forms.Timer _wifiTimer;

        private string pingTarget;
        private int pingInterval = 1000;
        private Icon? _defaultIcon;

        private ClipboardMonitorForm? _clipboardMonitorForm;
        private string? _lastNotifiedClipboardIp;
        private bool _clipboardMonitorEnabled;
        private readonly Dictionary<string, FloatingPingWindow> _floatingPingWindows = new();
        private TimeSpan _defaultAutoCloseDuration = TimeSpan.FromSeconds(10);

        private long _lastLatencyValue = -1;
        private bool _lastPingSuccess;
        private int _consecutiveFailedPings = 0;
        private bool _wasPreviouslyFailingPings = false;

        private readonly Font _fixedFont;
        private const float _fixedFontSize = 10f;

        private ToolStripMenuItem? _hostMenuItemGoogle, _hostMenuItemCloudflare, _hostMenuItemCustom;
        private ToolStripMenuItem? _clipboardMonitorMenuItem, _autoCloseMenuItem;
        private ToolStripMenuItem? _autoCloseMenuItem_3s, _autoCloseMenuItem_10s, _autoCloseMenuItem_30s, _autoCloseMenuItem_Never;

        private string _lastRoute = "";
        private bool _logRouteChangeEnabled;
        private readonly string _routeLogFilePath;

        private readonly string _appEventsLogFilePath;

        private volatile bool _isUpdatingWifi; 

        private ToolStripMenuItem? _dnsCheckMenuItem;
        private ToolStripMenuItem? _macAddressMenuItem;

        private ToolStripMenuItem? _wifiMenuItem;
        private ToolStripMenuItem? _wifiSignalItem;
        private ToolStripMenuItem? _wifiChannelItem;
        private ToolStripMenuItem? _wifiSpeedItem;
        private ToolStripMenuItem? _intervalMenuItem1Second, _intervalMenuItem3Seconds, _intervalMenuItem10Seconds;

        private bool _useTcpPing = false;
        private int _tcpPingPort = 443;
        private ToolStripMenuItem? _pingTypeMenuItem;
        private ToolStripMenuItem? _icmpPingMenuItem;
        private ToolStripMenuItem? _tcpPingMenuItem;
        private ToolStripMenuItem? _tcpPortMenuItem80, _tcpPortMenuItem443, _tcpPortMenuItemCustom;

        private ToolStripMenuItem? _scanPortsMenuItem;
        private readonly Dictionary<int, string> _commonTcpPortsToScan = new Dictionary<int, string>
        {
            { 21, "FTP" },
            { 22, "SSH" },
            { 23, "Telnet" },
            { 25, "SMTP" },
            { 53, "DNS" },
            { 80, "HTTP" },
            { 81, "HTTP (Alternativo)" },
            { 110, "POP3" },
            { 135, "RPC" },
            { 139, "NetBIOS Session" },
            { 143, "IMAP" },
            { 443, "HTTPS" },
            { 445, "SMB" },
            { 993, "IMAPS" },
            { 995, "POP3S" },
            { 1723, "PPTP" },
            { 3306, "MySQL" },
            { 3389, "RDP" },
            { 5900, "VNC" },
            { 5901, "VNC (Display 1)" },
            { 8015, "Custom" },
            { 8080, "HTTP Alt" },
            { 554, "RTSP (Streaming de Vídeo)" },
            { 1935, "RTMP (Streaming Flash/YouTube)" },
            { 3000, "Web App (Node.js/React Dev)" },
            { 5000, "Web API (Flask/Python)" },
            { 8000, "HTTP Dev/Alt" },
            { 8443, "HTTPS Alt (Painel Web SSL)" },
            { 1433, "Microsoft SQL Server" },
            { 1521, "Oracle DB" },
            { 27017, "MongoDB" },
            { 5432, "PostgreSQL" },
            { 6379, "Redis" },
            { 1194, "OpenVPN" },
            { 500, "IKE (IPSec VPN)" },
            { 1701, "L2TP (VPN)" },
            { 4500, "IPSec NAT-T" },
            { 161, "SNMP" },
            { 162, "SNMP Trap" },
            { 123, "NTP (Time Protocol)" },
        };
        private string _lastScannedIpOrHost = "N/A";
        private List<int> _lastScanOpenPorts = new();
        private bool _isScanningPorts = false;

        private List<string> _lastActiveConnections = new(); 
        private bool _isScanningLocalPorts = false;
        
        private ToolStripMenuItem? _localPortsMenuItem;

        private ToolStripMenuItem? _ipV4InfoMenuItem;
        private ToolStripMenuItem? _gatewayV4MenuItem;
        private ToolStripMenuItem? _privateIpV4SubMenuItem;
        private ToolStripMenuItem? _publicIpV4SubMenuItem;

        private ToolStripMenuItem? _ipV6InfoMenuItem;
        private ToolStripMenuItem? _gatewayV6MenuItem;
        private ToolStripMenuItem? _privateIpV6SubMenuItem;
        private ToolStripMenuItem? _publicIpV6SubMenuItem;

        private string _lastKnownPublicIpV4 = "N/A";
        private string _lastKnownPublicIpV6 = "N/A";
        private string _lastKnownPrivateIps = "N/A";
        private string _lastKnownPrivateIpV6s = "N/A";
        private string _lastKnownMacAddresses = "N/A";


        private readonly Queue<long> _latencyHistoryForIcon = new(PING_HISTORY_COUNT_ICON + 1);
        private const int PING_HISTORY_COUNT_ICON = 3;
        private bool _useAverageLatencyForIcon = true;

        private readonly Queue<long> _rawLatencySamples = new();
        private const int RAW_LATENCY_SAMPLE_COUNT = 10;
        private long _currentLatencyJitterMs = -1;

        private bool _networkSpeedMonitorEnabled = false;
        private PerformanceCounter? _uploadCounter;
        private PerformanceCounter? _downloadCounter;
        private double _currentUploadSpeed = 0;
        private double _currentDownloadSpeed = 0;
        private ToolStripMenuItem? _networkSpeedMonitorMenuItem;
        private ToolStripMenuItem? _networkInterfaceMenuItem;
        private NetworkInterface? _selectedNetworkInterface;
        private FloatingSpeedWindow? _floatingSpeedWindow;
        private List<string> _lastIpAddresses = new();
        
        private bool _speedWindowSingleLineMode = false;
        private ToolStripMenuItem? _speedWindowDisplayModeMenuItem;
        private ToolStripMenuItem? _speedWindowDisplayModeMultiLineItem;
        private ToolStripMenuItem? _speedWindowDisplayModeSingleLineItem;

        private AppSettings _settings = new();
        private readonly PingStatistics _pingStatistics = new(maxHistorySize: 100);
        private AlertSystem? _alertSystem;
        private readonly DnsCache _dnsCache = new(TimeSpan.FromMinutes(5));
        private ToolStripMenuItem? _statisticsMenuItem;
        private ToolStripMenuItem? _exportMenuItem;
        private ToolStripMenuItem? _alertsMenuItem;
        private ToolStripMenuItem? _resetStatsMenuItem;

        private LatencyHound? _latencyHound;
        private ToolStripMenuItem? _latencyHoundMenuItem;
        private bool _isLatencyHoundTracertRunning = false;

        private readonly List<(DateTime Timestamp, int ConsecutiveLosses)> _packetLossLog = new();



        private readonly WifiMonitor _wifiMonitor = new();

        public ISPing(string? initialTargetArg = null)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ISPing/2.1");
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string ispIngAppDataPath = Path.Combine(appDataPath, "ISPing");

            if (!Directory.Exists(ispIngAppDataPath))
            {
                try { Directory.CreateDirectory(ispIngAppDataPath); }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Erro ao criar diretório em AppData: {ex.Message}. Usando diretório base.");
                    ispIngAppDataPath = AppDomain.CurrentDomain.BaseDirectory;
                }
            }
            _routeLogFilePath = Path.Combine(ispIngAppDataPath, "ISPingRouteChanges.log");
            _appEventsLogFilePath = Path.Combine(ispIngAppDataPath, "ISPingAppEvents.log");
            LogAppEvent("Aplicação iniciada.", "INFO");
            
            _settings = AppSettings.Load();
            LogAppEvent("Configurações carregadas.");
            
            _alertSystem = new AlertSystem((title, msg, duration) => 
            {
                try { trayIcon?.ShowBalloonTip(duration, title, msg, ToolTipIcon.Info); }
                catch { }
            })
            {
                Enabled = _settings.AlertsEnabled,
                LatencyThresholdMs = _settings.LatencyAlertThresholdMs,
                ConsecutiveFailuresThreshold = _settings.ConsecutiveFailuresAlertThreshold,
                PlaySoundOnAlert = _settings.PlaySoundOnAlert
            };
            
            _latencyHound = new LatencyHound
            {
                Enabled = _settings.LatencyHoundEnabled,
                ThresholdMs = _settings.LatencyHoundThresholdMs,
                ConfirmationCount = _settings.LatencyHoundConfirmationCount,
                MinIntervalBetweenTracertsSeconds = _settings.LatencyHoundMinIntervalSeconds
            };
            LogAppEvent($"Latency Hound inicializado. Ativado: {_latencyHound.Enabled}, Threshold: {_latencyHound.ThresholdMs}ms, Confirmações: {_latencyHound.ConfirmationCount}");
            
            string initialHost = "8.8.8.8";
            if (!string.IsNullOrWhiteSpace(initialTargetArg) && IsValidHostnameOrIp(initialTargetArg)) 
                initialHost = initialTargetArg;
            else if (!string.IsNullOrWhiteSpace(_settings.PingTarget) && IsValidHostnameOrIp(_settings.PingTarget))
                initialHost = _settings.PingTarget;
            
            pingTarget = initialHost;
            pingInterval = _settings.PingInterval;
            _useTcpPing = _settings.UseTcpPing;
            _tcpPingPort = _settings.TcpPingPort;
            _useAverageLatencyForIcon = _settings.UseAverageLatencyForIcon;
            _defaultAutoCloseDuration = _settings.AutoCloseDuration;
            _clipboardMonitorEnabled = _settings.ClipboardMonitorEnabled;
            _logRouteChangeEnabled = _settings.LogRouteChangeEnabled;
            _networkSpeedMonitorEnabled = _settings.NetworkSpeedMonitorEnabled;
            _speedWindowSingleLineMode = _settings.SpeedWindowSingleLineMode;
            LogAppEvent($"Alvo inicial: {pingTarget}");
            _defaultIcon = LoadEmbeddedIcon("ISPing.isping.ico") ?? SystemIcons.Application;
            _fixedFont = EnsureFixedFontExists();
            trayIcon = new() { Icon = _defaultIcon, Text = $"Iniciando... (Alvo: {pingTarget})", Visible = true, ContextMenuStrip = CreateContextMenu() };
            UpdateIcon("...");
            pingTimer = new() { Interval = pingInterval };
            pingTimer.Tick += async (s, e) => await UpdatePingLatencyAndJitter();
            pingTimer.Start();
            InitializeClipboardMonitor();
            tracertTimer = new() { Interval = 120000, Enabled = _logRouteChangeEnabled };
            tracertTimer.Tick += async (s, e) => await CheckRouteChanges();
            networkSpeedTimer = new() { Interval = 1000, Enabled = _networkSpeedMonitorEnabled };
            networkSpeedTimer.Tick += (s, e) => UpdateNetworkSpeedAndWindow();
            _ipCheckTimer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
            _ipCheckTimer.Tick += async (s, e) => await UpdatePublicIpInfo();
            _ipCheckTimer.Start();
            _ipCheckTimer.Start();
            LogAppEvent("Timer de verificação de IP público iniciado (intervalo 30 min).");

            _wifiTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _wifiTimer.Tick += async (s, e) => await UpdateWifiInfoAsync();
            _wifiTimer.Start();
            
            _ = UpdatePingLatencyAndJitter();
            _ = UpdatePublicIpInfo();
            _ = UpdateWifiInfoAsync();
            UpdatePrivateIpV4Info();
            UpdatePrivateIpV6Info();
            UpdateMacAddressMenuItemText();
            _selectedNetworkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet);
            StoreCurrentIpAddresses();
            NetworkChange.NetworkAvailabilityChanged += NetworkAvailabilityChangedCallback;
            NetworkChange.NetworkAddressChanged += NetworkAddressChangedCallback;
            LogAppEvent("Monitores de eventos de rede registrados.");
            
            _ = ScanLocalPortsAsync();
        }

        public void LogAppEvent(string eventMessage, string level = "INFO")
        {
            try
            {
                // rotacao de log se > 5mb envia ao arquivo bak
                var fileInfo = new FileInfo(_appEventsLogFilePath);
                if (fileInfo.Exists && fileInfo.Length > 5 * 1024 * 1024)
                {
                    string backupPath = _appEventsLogFilePath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(_appEventsLogFilePath, backupPath);
                }

                string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToUpper()}] {eventMessage}{Environment.NewLine}";
                File.AppendAllText(_appEventsLogFilePath, logEntry);
            }
            catch (Exception ex) { Debug.WriteLine($"Falha ao escrever no log de eventos da aplicação: {ex.Message}"); }
        }

        private void NetworkAvailabilityChangedCallback(object? sender, NetworkAvailabilityEventArgs e)
        {
            string status = e.IsAvailable ? "CONECTADO" : "DESCONECTADO";
            LogAppEvent($"Disponibilidade da rede alterada: {status}", e.IsAvailable ? "INFO" : "WARN");
            trayIcon.ShowBalloonTip(3000, "Disponibilidade da Rede", $"A rede está agora {status.ToLower()}.", e.IsAvailable ? ToolTipIcon.Info : ToolTipIcon.Warning);
            if (!e.IsAvailable) { _consecutiveFailedPings = 1; _wasPreviouslyFailingPings = false; }
            _ = UpdatePingLatencyAndJitter();
            UpdateNetworkInterfaceMenuItems();
            _ = UpdatePublicIpInfo();
            UpdatePrivateIpV4Info();
            UpdatePrivateIpV6Info();
            UpdateMacAddressMenuItemText();
            _ = UpdateWifiInfoAsync();
        }

        private void NetworkAddressChangedCallback(object? sender, EventArgs e)
        {
            LogAppEvent("Endereço de rede alterado. Verificando interfaces...", "INFO");
            var currentIpAddresses = GetActiveIpAddresses();
            var newAddresses = currentIpAddresses.Except(_lastIpAddresses).ToList();
            var lostAddresses = _lastIpAddresses.Except(currentIpAddresses).ToList();
            bool significantChange = false;
            StringBuilder changeSummary = new("Mudança de endereço de rede detectada:\n");
            if (newAddresses.Any()) { significantChange = true; changeSummary.AppendLine($"Novos IPs: {string.Join(", ", newAddresses)}"); }
            if (lostAddresses.Any()) { significantChange = true; changeSummary.AppendLine($"IPs perdidos: {string.Join(", ", lostAddresses)}"); }
            var vpnKeywords = new[] { "vpn", "tap", "tun", "secure", "forti", "cisco", "openvpn", "sonicwall", "palo alto", "pulse" };
            var activeInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up);
            foreach (var ni in activeInterfaces)
            {
                if (vpnKeywords.Any(keyword => ni.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase) || ni.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    var vpnIps = ni.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).Select(addr => addr.Address.ToString());
                    if (vpnIps.Any(ip => newAddresses.Contains(ip)))
                    {
                        significantChange = true;
                        changeSummary.AppendLine($"Possível conexão VPN detectada na interface: {ni.Description} ({string.Join(", ", vpnIps)})");
                        LogAppEvent($"Possível conexão VPN detectada: {ni.Description} ({string.Join(", ", vpnIps)})", "INFO");
                        break;
                    }
                }
            }
            if (significantChange) trayIcon.ShowBalloonTip(4000, "Mudança de Endereço de Rede", changeSummary.ToString().TrimEnd(), ToolTipIcon.Info);
            _lastIpAddresses = currentIpAddresses;
            _ = UpdatePingLatencyAndJitter();
            _ = UpdateDnsMenuItemText();
            UpdateNetworkInterfaceMenuItems();
            _ = UpdatePublicIpInfo();
            UpdatePrivateIpV4Info();
            UpdatePrivateIpV6Info();
            UpdateMacAddressMenuItemText();
            _ = UpdateWifiInfoAsync();
        }

        private void StoreCurrentIpAddresses() { _lastIpAddresses = GetActiveIpAddresses(); }
        private List<string> GetActiveIpAddresses() => NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up).SelectMany(ni => ni.GetIPProperties().UnicastAddresses).Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).Select(addr => addr.Address.ToString()).Distinct().ToList();
        
        private string GetDefaultGateway(AddressFamily family)
        {
            try
            {
                var activeNi = NetworkInterface.GetAllNetworkInterfaces()
                    .FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                          (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                                           ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                                           ni.GetIPProperties().GatewayAddresses.Any(g => g.Address.AddressFamily == family));

                if (activeNi != null)
                {
                    var gateway = activeNi.GetIPProperties().GatewayAddresses
                        .FirstOrDefault(g => g.Address.AddressFamily == family)?.Address;
                    return gateway?.ToString() ?? "N/A";
                }
            }
            catch { }
            return "N/A";
        }

        private async Task<(string V4, string V6)> GetPublicIPAddressesAsync()
        {
            string ipv4 = "N/A", ipv6 = "N/A";
            try
            {
                string resultIpify = await _httpClient.GetStringAsync("https://api64.ipify.org");
                resultIpify = resultIpify.Trim();
                if (IPAddress.TryParse(resultIpify, out IPAddress? parsedIp))
                {
                    if (parsedIp.AddressFamily == AddressFamily.InterNetwork) ipv4 = parsedIp.ToString();
                    else if (parsedIp.AddressFamily == AddressFamily.InterNetworkV6) ipv6 = parsedIp.ToString();
                }
                else { LogAppEvent($"Resposta inesperada de api64.ipify.org: {resultIpify}", "WARN"); }
            }
            catch (Exception ex) { LogAppEvent($"Erro ao obter IP público (api64.ipify.org): {ex.Message}", "ERROR"); }
            if (ipv4 == "N/A")
            {
                try
                {
                    string resultIpifyV4 = await _httpClient.GetStringAsync("https://api.ipify.org");
                    resultIpifyV4 = resultIpifyV4.Trim();
                    if (IPAddress.TryParse(resultIpifyV4, out IPAddress? parsedIpV4) && parsedIpV4.AddressFamily == AddressFamily.InterNetwork) ipv4 = parsedIpV4.ToString();
                }
                catch (Exception ex) { LogAppEvent($"Erro ao obter IP público IPv4 (api.ipify.org): {ex.Message}", "ERROR"); }
            }
            if (ipv6 == "N/A")
            {
                try
                {
                    string resultIdentMeV6 = await _httpClient.GetStringAsync("https://v6.ident.me/");
                    resultIdentMeV6 = resultIdentMeV6.Trim();
                    if (IPAddress.TryParse(resultIdentMeV6, out IPAddress? parsedIpV6) && parsedIpV6.AddressFamily == AddressFamily.InterNetworkV6) ipv6 = parsedIpV6.ToString();
                }
                catch (Exception ex) { LogAppEvent($"Erro ao obter IP público IPv6 (v6.ident.me): {ex.Message}", "DEBUG"); }
            }
            return (ipv4, ipv6);
        }

        private async Task UpdatePublicIpInfo()
        {
            if (_publicIpV4SubMenuItem == null && _publicIpV6SubMenuItem == null) return;
            var (ipv4, ipv6) = await GetPublicIPAddressesAsync();
            if (ipv4 != "N/A" && ipv4 != _lastKnownPublicIpV4 && _lastKnownPublicIpV4 != "N/A") LogAppEvent($"IP Público IPv4 alterado: De '{_lastKnownPublicIpV4}' para '{ipv4}'");
            _lastKnownPublicIpV4 = ipv4;
            _alertSystem?.CheckPublicIpChange(ipv4);
            if (ipv6 != "N/A" && ipv6 != _lastKnownPublicIpV6 && _lastKnownPublicIpV6 != "N/A") LogAppEvent($"IP Público IPv6 alterado: De '{_lastKnownPublicIpV6}' para '{ipv6}'");
            _lastKnownPublicIpV6 = ipv6;
            Action updateAction = () =>
            {
                if (_publicIpV4SubMenuItem != null && !_publicIpV4SubMenuItem.IsDisposed)
                {
                    _publicIpV4SubMenuItem.Text = $"Público: {_lastKnownPublicIpV4}";
                    _publicIpV4SubMenuItem.Tag = (_lastKnownPublicIpV4 != "N/A" && IPAddress.TryParse(_lastKnownPublicIpV4, out _)) ? _lastKnownPublicIpV4 : null;
                    _publicIpV4SubMenuItem.Enabled = _publicIpV4SubMenuItem.Tag != null;
                }
                if (_publicIpV6SubMenuItem != null && !_publicIpV6SubMenuItem.IsDisposed)
                {
                    _publicIpV6SubMenuItem.Text = $"Público: {_lastKnownPublicIpV6}";
                    _publicIpV6SubMenuItem.Tag = (_lastKnownPublicIpV6 != "N/A" && IPAddress.TryParse(_lastKnownPublicIpV6, out _)) ? _lastKnownPublicIpV6 : null;
                    _publicIpV6SubMenuItem.Enabled = _publicIpV6SubMenuItem.Tag != null;
                }
                UpdateIpInfoMenuItemStates();
            };

            if (trayIcon.ContextMenuStrip?.InvokeRequired ?? false) { try { trayIcon.ContextMenuStrip.Invoke(updateAction); } catch (InvalidOperationException) { } }
            else { updateAction(); }

            // esconder ipv6
            Action visibilityAction = () =>
            {
                if (_ipV6InfoMenuItem != null && !_ipV6InfoMenuItem.IsDisposed)
                {
                    _ipV6InfoMenuItem.Visible = (ipv6 != "N/A" && ipv6 != "");
                }
            };
            if (trayIcon.ContextMenuStrip?.InvokeRequired ?? false) { try { trayIcon.ContextMenuStrip.Invoke(visibilityAction); } catch { } }
            else { visibilityAction(); }
        }

        private void UpdatePrivateIpV4Info()
        {
            if (_privateIpV4SubMenuItem == null) return;
            
            Task.Run(() => 
            {
                try
                {
                    var privateIpsList = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                     (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                                      ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                        .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                        .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        .Select(addr => addr.Address.ToString())
                        .Distinct()
                        .ToList();
                        
                    string currentPrivateIpsText = privateIpsList.Any() ? string.Join(", ", privateIpsList) : "N/A";
                    object? tagData = privateIpsList.Any() ? string.Join(Environment.NewLine, privateIpsList) : null;
                    
                    if (currentPrivateIpsText != _lastKnownPrivateIps && _lastKnownPrivateIps != "N/A") 
                        LogAppEvent($"IP(s) Privado(s) IPv4 alterado(s): De '{_lastKnownPrivateIps}' para '{currentPrivateIpsText}'");
                    
                    _lastKnownPrivateIps = currentPrivateIpsText;
                    
                    string gateway = GetDefaultGateway(AddressFamily.InterNetwork);

                    Action updateAction = () =>
                    {
                        if (_gatewayV4MenuItem != null && !_gatewayV4MenuItem.IsDisposed)
                        {
                            _gatewayV4MenuItem.Text = $"Gateway: {gateway}";
                            _gatewayV4MenuItem.Tag = (gateway != "N/A") ? gateway : null;
                            _gatewayV4MenuItem.Enabled = _gatewayV4MenuItem.Tag != null;
                        }
                        if (!_privateIpV4SubMenuItem.IsDisposed)
                        {
                            _privateIpV4SubMenuItem.Text = $"Privado: {currentPrivateIpsText}";
                            _privateIpV4SubMenuItem.Tag = tagData;
                            _privateIpV4SubMenuItem.Enabled = tagData != null;
                        }
                        UpdateIpInfoMenuItemStates();
                    };
                    
                    if (trayIcon.ContextMenuStrip?.IsHandleCreated ?? false) 
                    { 
                        try { trayIcon.ContextMenuStrip.BeginInvoke(updateAction); } catch { } 
                    }
                }
                catch (Exception ex) { LogAppEvent($"Erro em UpdatePrivateIpV4Info: {ex.Message}", "ERROR"); }
            });
        }

        private void UpdatePrivateIpV6Info()
        {
            if (_privateIpV6SubMenuItem == null) return;

            Task.Run(() =>
            {
                try
                {
                    var privateIpV6List = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                                     (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                      ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                        .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                        .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                                       !IPAddress.IsLoopback(addr.Address) &&
                                       (addr.Address.IsIPv6LinkLocal || addr.Address.IsIPv6SiteLocal || addr.Address.IsIPv6UniqueLocal || (!addr.Address.IsIPv6Multicast && !addr.Address.IsIPv6Teredo)))
                        .Select(addr => addr.Address.ToString().Split('%')[0]) 
                        .Distinct()
                        .ToList();

                    string currentPrivateIpV6sText = privateIpV6List.Any() ? string.Join(", ", privateIpV6List) : "N/A";
                    object? tagData = privateIpV6List.Any() ? string.Join(Environment.NewLine, privateIpV6List) : null;

                    if (currentPrivateIpV6sText != _lastKnownPrivateIpV6s && _lastKnownPrivateIpV6s != "N/A")
                    {
                        LogAppEvent($"IP(s) Privado(s) IPv6 alterado(s): De '{_lastKnownPrivateIpV6s}' para '{currentPrivateIpV6sText}'");
                    }
                    _lastKnownPrivateIpV6s = currentPrivateIpV6sText;
                    
                    string gateway = GetDefaultGateway(AddressFamily.InterNetworkV6);

                    Action updateAction = () =>
                    {
                        if (!_privateIpV6SubMenuItem.IsDisposed)
                        {
                            if (_gatewayV6MenuItem != null && !_gatewayV6MenuItem.IsDisposed)
                            {
                                _gatewayV6MenuItem.Text = $"Gateway: {gateway}";
                                _gatewayV6MenuItem.Tag = (gateway != "N/A") ? gateway : null;
                                _gatewayV6MenuItem.Enabled = _gatewayV6MenuItem.Tag != null;
                            }
                            _privateIpV6SubMenuItem.Text = $"Privado: {currentPrivateIpV6sText}";
                            _privateIpV6SubMenuItem.Tag = tagData;
                            _privateIpV6SubMenuItem.Enabled = tagData != null;
                        }
                        UpdateIpInfoMenuItemStates();
                    };
                    
                    if (trayIcon.ContextMenuStrip?.IsHandleCreated ?? false)
                    {
                        try { trayIcon.ContextMenuStrip.BeginInvoke(updateAction); } catch { }
                    }
                }
                catch (Exception ex) { LogAppEvent($"Erro em UpdatePrivateIpV6Info: {ex.Message}", "ERROR"); }
            });
        }

        private void UpdateIpInfoMenuItemStates()
        {
            if (_ipV4InfoMenuItem != null && _privateIpV4SubMenuItem != null && _publicIpV4SubMenuItem != null)
            {
                _ipV4InfoMenuItem.Enabled = _privateIpV4SubMenuItem.Enabled || _publicIpV4SubMenuItem.Enabled;
            }
            if (_ipV6InfoMenuItem != null && _privateIpV6SubMenuItem != null && _publicIpV6SubMenuItem != null)
            {
                bool privateV6Available = _privateIpV6SubMenuItem.Enabled;
                bool publicV6Available = _publicIpV6SubMenuItem.Enabled;
                
                _ipV6InfoMenuItem.Enabled = privateV6Available || publicV6Available;
            }
        }


        private void UpdateMacAddressMenuItemText()
        {
            if (_macAddressMenuItem == null) return;

            var activeMacsList = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up &&
                             (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                              ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                .Select(ni => ni.GetPhysicalAddress())
                .Where(pa => pa != null && pa.ToString() != string.Empty)
                .Select(pa => this.FormatPhysicalAddress(pa))
                .Distinct()
                .ToList();

            string currentMacsText = activeMacsList.Any() ? string.Join(", ", activeMacsList) : "N/A";
            object? tagData = activeMacsList.Any() ? string.Join(Environment.NewLine, activeMacsList) : null;

            if (currentMacsText != _lastKnownMacAddresses && _lastKnownMacAddresses != "N/A")
            {
                LogAppEvent($"MAC Address(es) alterado(s): De '{_lastKnownMacAddresses}' para '{currentMacsText}'");
            }
            _lastKnownMacAddresses = currentMacsText;

            Action updateAction = () =>
            {
                if (!_macAddressMenuItem.IsDisposed)
                {
                    _macAddressMenuItem.Text = $"MAC: {currentMacsText}";
                    _macAddressMenuItem.Tag = tagData;
                    _macAddressMenuItem.Enabled = tagData != null;
                }
            };

            if (_macAddressMenuItem.GetCurrentParent() is ContextMenuStrip parentMenu && parentMenu.InvokeRequired)
            {
                try { parentMenu.Invoke(updateAction); } catch (InvalidOperationException) { }
            }
            else
            {
                updateAction();
            }
        }

        private string FormatPhysicalAddress(PhysicalAddress address)
        {
            if (address == null) return string.Empty;
            byte[] bytes = address.GetAddressBytes();
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return string.Join("-", bytes.Select(b => b.ToString("X2")));
        }

        private async Task UpdateWifiInfoAsync()
        {
            if (_wifiMenuItem == null) return;
            if (_isUpdatingWifi) return;
            
            _isUpdatingWifi = true;
            await Task.Run(() =>
            {
                try
                {
                    var wifiInfo = _wifiMonitor.GetWifiInfo();
                    string menuText = "";
                    string signalText = "Sinal: N/A";
                    string channelText = "Canal: N/A";
                    string speedText = "Velocidade: N/A";
                    bool showSubItems = false;

                    if (wifiInfo.IsConnected)
                    {
                         menuText = $"Wi-Fi: {wifiInfo.Ssid}";
                         
                         signalText = $"Sinal: {wifiInfo.SignalStrength} ({wifiInfo.Rssi}dBm)";
                         channelText = $"Canal: {wifiInfo.Channel} ({wifiInfo.FrequencyBand})";
                         speedText = $"Velocidade: {(string.IsNullOrEmpty(wifiInfo.LinkSpeed) ? "N/A" : wifiInfo.LinkSpeed)}";
                         showSubItems = true;
                    }
                    else
                    {
                        var ethernet = NetworkInterface.GetAllNetworkInterfaces()
                            .FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet && 
                                                  ni.OperationalStatus == OperationalStatus.Up);

                        if (ethernet != null)
                        {
                            long speedMbps = ethernet.Speed / 1000000;
                            menuText = $"Conexão: Ethernet ({speedMbps} Mbps)";
                        }
                        else
                        {
                            menuText = "Sem acesso a internet";
                        }
                        showSubItems = false;
                    }

                    Action updateAction = () =>
                    {
                        if (!_wifiMenuItem.IsDisposed)
                        {
                            _wifiMenuItem.Text = menuText;
                            _wifiMenuItem.Visible = true;
                            
                            if (_wifiSignalItem != null) 
                            {
                                _wifiSignalItem.Text = signalText;
                                _wifiSignalItem.Visible = showSubItems;
                            }
                            if (_wifiChannelItem != null)
                            {
                                _wifiChannelItem.Text = channelText;
                                _wifiChannelItem.Visible = showSubItems;
                            }
                            if (_wifiSpeedItem != null)
                            {
                                _wifiSpeedItem.Text = speedText;
                                _wifiSpeedItem.Visible = showSubItems;
                            }
                        }
                    };

                    if (trayIcon.ContextMenuStrip?.IsHandleCreated ?? false)
                    {
                        try { trayIcon.ContextMenuStrip.BeginInvoke(updateAction); } catch { }
                    }
                }
                finally { _isUpdatingWifi = false; }
            });
        }


        private void CopyIpToClipboard_Click(object? sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem { Tag: string ipAddress })
            {
                try
                {
                    Clipboard.SetText(ipAddress);
                    string type = "IP";
                    if (sender == _publicIpV4SubMenuItem) type = "IP Público IPv4";
                    else if (sender == _publicIpV6SubMenuItem) type = "IP Público IPv6";
                    else if (sender == _privateIpV4SubMenuItem) type = "IP Privado IPv4";
                    else if (sender == _privateIpV6SubMenuItem) type = "IP Privado IPv6";
                    else if (sender == _macAddressMenuItem) type = "MAC Address";

                    LogAppEvent($"{type} '{ipAddress.Replace(Environment.NewLine, ", ")}' copiado.");
                    trayIcon.ShowBalloonTip(1500, $"{type} Copiado", $"{ipAddress.Replace(Environment.NewLine, ", ")} copiado!", ToolTipIcon.Info);
                }
                catch (Exception ex)
                {
                    LogAppEvent($"Erro ao copiar {((sender as ToolStripMenuItem)?.Text ?? "valor")}: {ex.Message}", "ERROR");
                    MessageBox.Show($"Erro ao copiar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private Font EnsureFixedFontExists()
        {
            using var installedFonts = new InstalledFontCollection();
            string fontName = installedFonts.Families.Any(f => f.Name.Equals("Arial", StringComparison.OrdinalIgnoreCase)) ? "Arial" : FontFamily.GenericSansSerif.Name;
            return new Font(fontName, _fixedFontSize, FontStyle.Bold);
        }

        private void InitializeClipboardMonitor()
        {
            _clipboardMonitorForm = new ClipboardMonitorForm(this);
            if (!_clipboardMonitorForm.IsHandleCreated) _ = _clipboardMonitorForm.Handle;
            if (_clipboardMonitorForm.IsHandleCreated) AddClipboardFormatListener(_clipboardMonitorForm.Handle);
        }

        internal void HandleClipboardUpdate()
        {
            if (!_clipboardMonitorEnabled || !Clipboard.ContainsText()) return;
            try
            {
                string clipboardText = Clipboard.GetText().Trim();
                if (IsValidDottedDecimalIPAddress(clipboardText) && clipboardText != _lastNotifiedClipboardIp && clipboardText != pingTarget)
                {
                    _lastNotifiedClipboardIp = clipboardText;
                    LogAppEvent($"IP detectado na área de transferência: {clipboardText}. Criando janela flutuante.");
                    CreateOrShowFloatingPingWindow(clipboardText);
                }
            }
            catch (Exception ex) { LogAppEvent($"Erro ao processar atualização da área de transferência: {ex.Message}", "ERROR"); }
        }

        internal static bool IsValidDottedDecimalIPAddress(string ipString) => IPAddress.TryParse(ipString, out IPAddress? address) && address.AddressFamily == AddressFamily.InterNetwork;
        internal static bool IsValidHostnameOrIp(string hostOrIp) => !string.IsNullOrWhiteSpace(hostOrIp) && (IsValidDottedDecimalIPAddress(hostOrIp) || Uri.CheckHostName(hostOrIp) != UriHostNameType.Unknown);

        private void CreateOrShowFloatingPingWindow(string ipAddress)
        {
            if (!IsValidDottedDecimalIPAddress(ipAddress)) { LogAppEvent($"Tentativa de criar janela flutuante para IP inválido: {ipAddress}", "WARN"); return; }
            string windowKey = ipAddress;
            if (_floatingPingWindows.TryGetValue(windowKey, out FloatingPingWindow? window)) window.Activate();
            else
            {
                FloatingPingWindow floatingWindow = new(ipAddress, _defaultAutoCloseDuration);
                floatingWindow.FormClosed += (s, e) => _floatingPingWindows.Remove(windowKey);
                _floatingPingWindows.Add(windowKey, floatingWindow);
                floatingWindow.Show();
            }
        }

        private Icon? LoadEmbeddedIcon(string resourceName)
        {
            try
            {
                using Stream? resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                return resourceStream != null ? new Icon(resourceStream) : null;
            }
            catch (Exception ex) { LogAppEvent($"Erro ao carregar ícone embutido '{resourceName}': {ex.Message}", "ERROR"); return null; }
        }

        private ContextMenuStrip CreateContextMenu()
        {
            var contextMenu = new ContextMenuStrip();
            
            _wifiMenuItem = new ToolStripMenuItem("Wi-Fi: Inicializando...");
            _wifiSignalItem = new ToolStripMenuItem("Sinal: N/A");
            _wifiChannelItem = new ToolStripMenuItem("Canal: N/A");
            _wifiSpeedItem = new ToolStripMenuItem("Velocidade: N/A");
            
            _wifiMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _wifiSignalItem, _wifiChannelItem, _wifiSpeedItem });

            _wifiMenuItem.Click += (s, e) => 
            {
                if (!string.IsNullOrEmpty(_wifiMenuItem.Text))
                {
                    Clipboard.SetText(_wifiMenuItem.Text.Replace("Wi-Fi: ", ""));
                }
            };
            
            var targetItem = new ToolStripMenuItem("Alvo do Ping");
            _hostMenuItemGoogle = new ToolStripMenuItem("8.8.8.8 (Google)", null, (s, e) => SetPingTarget("8.8.8.8"));
            _hostMenuItemCloudflare = new ToolStripMenuItem("1.1.1.1 (Cloudflare)", null, (s, e) => SetPingTarget("1.1.1.1"));
            _hostMenuItemCustom = new ToolStripMenuItem("Personalizado...", null, (s, e) => SetCustomPingTarget());
            targetItem.DropDownItems.AddRange(new[] { _hostMenuItemGoogle, _hostMenuItemCloudflare, _hostMenuItemCustom });

            _pingTypeMenuItem = new ToolStripMenuItem("Tipo de Ping");
            _icmpPingMenuItem = new ToolStripMenuItem("ICMP (Padrão)", null, (s, e) => SetPingType(false)) { CheckOnClick = true };
            _tcpPingMenuItem = new ToolStripMenuItem("TCP", null, (s, e) => SetPingType(true)) { CheckOnClick = true };
            
            var tcpPortSubMenu = new ToolStripMenuItem("Porta TCP");
            _tcpPortMenuItem80 = new ToolStripMenuItem("80 (HTTP)", null, (s, e) => SetTcpPingPortAndDisplay(80)) { CheckOnClick = true };
            _tcpPortMenuItem443 = new ToolStripMenuItem("443 (HTTPS)", null, (s, e) => SetTcpPingPortAndDisplay(443)) { CheckOnClick = true };
            _tcpPortMenuItemCustom = new ToolStripMenuItem("Personalizado...", null, (s, e) => SetCustomTcpPingPort());
            tcpPortSubMenu.DropDownItems.AddRange(new[] { _tcpPortMenuItem80, _tcpPortMenuItem443, _tcpPortMenuItemCustom });
            
            _tcpPingMenuItem.DropDownItems.Add(tcpPortSubMenu);
            _pingTypeMenuItem.DropDownItems.AddRange(new[] { _icmpPingMenuItem, _tcpPingMenuItem });

            _scanPortsMenuItem = new ToolStripMenuItem("Escanear Portas");
            _scanPortsMenuItem.DropDownOpening += ScanPortsMenuItem_DropDownOpening;
            _scanPortsMenuItem.Enabled = IsValidHostnameOrIp(pingTarget);

            _localPortsMenuItem = new ToolStripMenuItem("Portas Locais: Escaneando...");
            _localPortsMenuItem.DropDownOpening += LocalPortsMenuItem_DropDownOpening;

            var intervalItem = new ToolStripMenuItem("Intervalo");
            _intervalMenuItem1Second = new ToolStripMenuItem("1 segundo (padrão)", null, (s, e) => SetPingInterval(1000));
            _intervalMenuItem3Seconds = new ToolStripMenuItem("3 segundos", null, (s, e) => SetPingInterval(3000));
            _intervalMenuItem10Seconds = new ToolStripMenuItem("10 segundos", null, (s, e) => SetPingInterval(10000));
            intervalItem.DropDownItems.AddRange(new[] { _intervalMenuItem1Second, _intervalMenuItem3Seconds, _intervalMenuItem10Seconds });
            var displayModeItem = new ToolStripMenuItem("Exibição do Ping (Ícone)");
            displayModeItem.DropDownItems.AddRange(new ToolStripMenuItem[] { new("Último Ping", null, (s, e) => SetPingDisplayModeForIcon(false)), new($"Média dos Últimos {PING_HISTORY_COUNT_ICON} Pings", null, (s, e) => SetPingDisplayModeForIcon(true)) });
            _clipboardMonitorMenuItem = new ToolStripMenuItem("Monitorar IPs na Área de Transferência") { CheckOnClick = true, Checked = _clipboardMonitorEnabled };
            _clipboardMonitorMenuItem.CheckedChanged += (s, e) => { _clipboardMonitorEnabled = ((ToolStripMenuItem)s!).Checked; LogAppEvent($"Monitor de IPs {(_clipboardMonitorEnabled ? "ATIVADO" : "DESATIVADO")}."); };
            _autoCloseMenuItem = new ToolStripMenuItem("Auto-Fechar Janelas Flutuantes de Ping");
            _autoCloseMenuItem_3s = new ToolStripMenuItem("3 segundos", null, (s, e) => SetFloatingWindowAutoCloseDuration(TimeSpan.FromSeconds(3)));
            _autoCloseMenuItem_10s = new ToolStripMenuItem("10 segundos (padrão)", null, (s, e) => SetFloatingWindowAutoCloseDuration(TimeSpan.FromSeconds(10)));
            _autoCloseMenuItem_30s = new ToolStripMenuItem("30 segundos", null, (s, e) => SetFloatingWindowAutoCloseDuration(TimeSpan.FromSeconds(30)));
            _autoCloseMenuItem_Never = new ToolStripMenuItem("Nunca", null, (s, e) => SetFloatingWindowAutoCloseDuration(TimeSpan.Zero));
            _autoCloseMenuItem.DropDownItems.AddRange(new[] { _autoCloseMenuItem_3s, _autoCloseMenuItem_10s, _autoCloseMenuItem_30s, _autoCloseMenuItem_Never });

            _networkSpeedMonitorMenuItem = new ToolStripMenuItem("Monitorar Velocidade de Rede/Jitter") { CheckOnClick = true, Checked = _networkSpeedMonitorEnabled };
            _networkSpeedMonitorMenuItem.CheckedChanged += (s, e) => SetNetworkSpeedMonitorEnabled(((ToolStripMenuItem)s!).Checked);
            _networkInterfaceMenuItem = new ToolStripMenuItem("Interface de Rede");
            
            _speedWindowDisplayModeMenuItem = new ToolStripMenuItem("Formato Janela Velocidade");
            _speedWindowDisplayModeMultiLineItem = new ToolStripMenuItem("Múltiplas Linhas (Padrão)", null, (s,e) => SetSpeedWindowDisplayMode(false)) { CheckOnClick = true };
            _speedWindowDisplayModeSingleLineItem = new ToolStripMenuItem("Linha Única", null, (s,e) => SetSpeedWindowDisplayMode(true)) { CheckOnClick = true };
            _speedWindowDisplayModeMenuItem.DropDownItems.AddRange(new ToolStripItem[] { _speedWindowDisplayModeMultiLineItem, _speedWindowDisplayModeSingleLineItem });


            _dnsCheckMenuItem = new ToolStripMenuItem("DNS: Checando...");
            _macAddressMenuItem = new ToolStripMenuItem("MAC: Checando...") { Enabled = false };
            _macAddressMenuItem.Click += CopyIpToClipboard_Click;

            _ipV4InfoMenuItem = new ToolStripMenuItem("Endereços IPv4") { Enabled = false };
            _gatewayV4MenuItem = new ToolStripMenuItem("Gateway: Checando...") { Enabled = false };
            _gatewayV4MenuItem.Click += CopyIpToClipboard_Click;
            _privateIpV4SubMenuItem = new ToolStripMenuItem("Privado: Checando...") { Enabled = false }; 
            _privateIpV4SubMenuItem.Click += CopyIpToClipboard_Click;
            _publicIpV4SubMenuItem = new ToolStripMenuItem("Público: Checando...") { Enabled = false }; 
            _publicIpV4SubMenuItem.Click += CopyIpToClipboard_Click;
            _ipV4InfoMenuItem.DropDownItems.AddRange(new[] { _gatewayV4MenuItem, _privateIpV4SubMenuItem, _publicIpV4SubMenuItem });

            _ipV6InfoMenuItem = new ToolStripMenuItem("Endereços IPv6") { Enabled = false, Visible = true }; 
            _gatewayV6MenuItem = new ToolStripMenuItem("Gateway: Checando...") { Enabled = false };
            _gatewayV6MenuItem.Click += CopyIpToClipboard_Click;
            _privateIpV6SubMenuItem = new ToolStripMenuItem("Privado: Checando...") { Enabled = false, Visible = true }; 
            _privateIpV6SubMenuItem.Click += CopyIpToClipboard_Click;
            _publicIpV6SubMenuItem = new ToolStripMenuItem("Público: Checando...") { Enabled = false, Visible = true }; 
            _publicIpV6SubMenuItem.Click += CopyIpToClipboard_Click;
            _ipV6InfoMenuItem.DropDownItems.AddRange(new[] { _gatewayV6MenuItem, _privateIpV6SubMenuItem, _publicIpV6SubMenuItem });

            _statisticsMenuItem = new ToolStripMenuItem("Ver Estatísticas...", null, (s, e) => ShowStatistics());
            _resetStatsMenuItem = new ToolStripMenuItem("Resetar Estatísticas", null, (s, e) => ResetStatistics());
            
            _exportMenuItem = new ToolStripMenuItem("Exportar Dados");
            var exportCsvItem = new ToolStripMenuItem("Exportar para CSV...", null, (s, e) => ExportDataToCsv());
            var exportJsonItem = new ToolStripMenuItem("Exportar para JSON...", null, (s, e) => ExportDataToJson());
            _exportMenuItem.DropDownItems.AddRange(new[] { exportCsvItem, exportJsonItem });
            
            _statisticsMenuItem.DropDownItems.Add(new ToolStripSeparator());
            _statisticsMenuItem.DropDownItems.Add(_exportMenuItem);
            
            _alertsMenuItem = new ToolStripMenuItem("Configurar Alertas...", null, (s, e) => ConfigureAlerts());

            _latencyHoundMenuItem = new ToolStripMenuItem("Latency Hound");
            var latencyHoundEnableItem = new ToolStripMenuItem("Ativado") 
            { 
                CheckOnClick = true, 
                Checked = _latencyHound?.Enabled ?? true 
            };
            latencyHoundEnableItem.CheckedChanged += (s, e) => 
            {
                if (_latencyHound != null)
                {
                    _latencyHound.Enabled = latencyHoundEnableItem.Checked;
                    LogAppEvent($"Latency Hound {(_latencyHound.Enabled ? "ATIVADO" : "DESATIVADO")}.");
                }
            };
            var latencyHoundViewResultsItem = new ToolStripMenuItem("Ver Histórico de Tracert...", null, (s, e) => ShowLatencyHoundResults());
            var latencyHoundConfigItem = new ToolStripMenuItem("Configurar...", null, (s, e) => ConfigureLatencyHound());
            var latencyHoundRunNowItem = new ToolStripMenuItem("Executar Tracert Agora", null, async (s, e) => await RunLatencyHoundTracertManually());
            _latencyHoundMenuItem.DropDownItems.AddRange(new ToolStripItem[] 
            { 
                latencyHoundEnableItem, 
                new ToolStripSeparator(),
                latencyHoundViewResultsItem, 
                latencyHoundConfigItem,
                new ToolStripSeparator(),
                latencyHoundRunNowItem 
            });

            contextMenu.Items.AddRange(new ToolStripItem[] {
                _wifiMenuItem,
                new ToolStripSeparator(),
                targetItem,
                _pingTypeMenuItem,
                _scanPortsMenuItem,
                _localPortsMenuItem,
                new ToolStripSeparator(),
                intervalItem, displayModeItem, new ToolStripSeparator(),
                _clipboardMonitorMenuItem, _autoCloseMenuItem, new ToolStripSeparator(),
                _networkSpeedMonitorMenuItem, _networkInterfaceMenuItem, _speedWindowDisplayModeMenuItem,
                new ToolStripSeparator(),

                _dnsCheckMenuItem, _ipV4InfoMenuItem, _ipV6InfoMenuItem, _macAddressMenuItem, new ToolStripSeparator(),
                _statisticsMenuItem!, _alertsMenuItem!, _latencyHoundMenuItem!, new ToolStripSeparator(),
                new ToolStripMenuItem("Sobre...", null, (s, e) => new AboutForm().ShowDialog()),
                new ToolStripMenuItem("Sair", null, (s, e) => ExitApplication())
            });
            contextMenu.Opening += ContextMenu_Opening;
            return contextMenu;
        }

        private void SetPingType(bool useTcp)
        {
            if (_useTcpPing == useTcp) return;

            _useTcpPing = useTcp;
            LogAppEvent($"Tipo de ping alterado para: {(_useTcpPing ? "TCP" : "ICMP")}.");
            ResetPingState();

            if (_icmpPingMenuItem != null) _icmpPingMenuItem.Checked = !_useTcpPing;
            if (_tcpPingMenuItem != null) _tcpPingMenuItem.Checked = _useTcpPing;
            
            _ = UpdatePingLatencyAndJitter();
        }

        private void SetTcpPingPort(int port)
        {
            if (_tcpPingPort == port) return;
            _tcpPingPort = port;
            LogAppEvent($"Porta TCP para ping alterada para: {port}.");

            if (_tcpPortMenuItem80 != null) _tcpPortMenuItem80.Checked = (port == 80);
            if (_tcpPortMenuItem443 != null) _tcpPortMenuItem443.Checked = (port == 443);
            if (_tcpPortMenuItemCustom != null) _tcpPortMenuItemCustom.Checked = (port != 80 && port != 443);

            if (_useTcpPing)
            {
                ResetPingState();
                _ = UpdatePingLatencyAndJitter();
            }
        }

        private void SetTcpPingPortAndDisplay(int port)
        {
            SetTcpPingPort(port);
            SetPingType(true);
            trayIcon.ShowBalloonTip(1500, "Porta TCP Definida", $"Ping TCP agora usa porta {port}.", ToolTipIcon.Info);
        }

        private void SetCustomTcpPingPort()
        {
            string? input = Interaction.InputBox($"Digite a porta TCP para o ping (atual: {_tcpPingPort}):", "Porta TCP Personalizada", _tcpPingPort.ToString());
            if (string.IsNullOrWhiteSpace(input)) return;

            if (int.TryParse(input, out int newPort) && newPort > 0 && newPort <= 65535)
            {
                SetTcpPingPortAndDisplay(newPort);
            }
            else
            {
                LogAppEvent($"Tentativa de definir porta TCP inválida: {input}", "WARN");
                MessageBox.Show("A porta deve ser um número entre 1 e 65535.", "Porta Inválida", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void SetSpeedWindowDisplayMode(bool singleLineMode)
        {
            if (_speedWindowSingleLineMode == singleLineMode) return;

            _speedWindowSingleLineMode = singleLineMode;
            LogAppEvent($"Formato da janela de velocidade alterado para: {(_speedWindowSingleLineMode ? "Linha Única" : "Múltiplas Linhas")}.");

            if (_floatingSpeedWindow != null && !_floatingSpeedWindow.IsDisposed)
            {
                _floatingSpeedWindow.SetDisplayMode(_speedWindowSingleLineMode);
                UpdateFloatingSpeedWindowContent();
            }
            if (_speedWindowDisplayModeMultiLineItem != null) _speedWindowDisplayModeMultiLineItem.Checked = !_speedWindowSingleLineMode;
            if (_speedWindowDisplayModeSingleLineItem != null) _speedWindowDisplayModeSingleLineItem.Checked = _speedWindowSingleLineMode;
        }

        private void SetPingDisplayModeForIcon(bool useAverage)
        {
            _useAverageLatencyForIcon = useAverage;
            LogAppEvent($"Exibição do ícone: {(_useAverageLatencyForIcon ? "Média" : "Último Ping")}.");
            _ = UpdatePingLatencyAndJitter();
        }

        private void SetFloatingWindowAutoCloseDuration(TimeSpan newDuration)
        {
            _defaultAutoCloseDuration = newDuration;
            LogAppEvent($"Auto-fechar janela flutuante: {newDuration.TotalSeconds}s.");
            foreach (var window in _floatingPingWindows.Values.ToList()) window.SetAutoCloseDuration(newDuration);
        }

        private async Task UpdateDnsMenuItemText()
        {
            if (_dnsCheckMenuItem == null) return;
            string dnsServerAddress = GetCurrentDnsServerAddress();
            string dnsLookupResult = string.IsNullOrEmpty(dnsServerAddress) ? "N/A" : await PerformDnsLookup(dnsServerAddress);
            string dnsMenuItemText = string.IsNullOrEmpty(dnsServerAddress) ? "DNS: Não Configurado" : $"DNS: {dnsServerAddress} ({dnsLookupResult})";
            if (!_dnsCheckMenuItem.IsDisposed && _dnsCheckMenuItem.GetCurrentParent() is ContextMenuStrip parentMenu) { try { parentMenu.Invoke(new Action(() => { if (!_dnsCheckMenuItem.IsDisposed) _dnsCheckMenuItem.Text = dnsMenuItemText; })); } catch (InvalidOperationException) { } }
        }

        private string GetCurrentDnsServerAddress() => NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet).Select(ni => ni.GetIPProperties()?.DnsAddresses.FirstOrDefault(dns => dns.AddressFamily == AddressFamily.InterNetwork)).FirstOrDefault(dns => dns != null)?.ToString() ?? string.Empty;

        private async Task<string> PerformDnsLookup(string ipAddress)
        {
            try { IPHostEntry hostEntry = await Dns.GetHostEntryAsync(ipAddress); return hostEntry.HostName != ipAddress ? hostEntry.HostName : "Hostname não encontrado."; }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.HostNotFound) { LogAppEvent($"DNS Lookup para {ipAddress}: Hostname não encontrado.", "WARN"); return "Hostname não encontrado."; }
            catch (Exception ex) { LogAppEvent($"DNS Lookup para {ipAddress} falhou: {ex.Message}", "ERROR"); return "Erro DNS Lookup."; }
        }

        private void ViewRouteLog()
        {
            try
            {
                if (!File.Exists(_routeLogFilePath)) { MessageBox.Show("Nenhum log de rotas encontrado.", "Log Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                string logContent = File.ReadAllText(_routeLogFilePath);
                if (string.IsNullOrWhiteSpace(logContent)) { MessageBox.Show("O log de rotas está vazio.", "Log Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                using var logForm = new LogDisplayForm("Log de Mudanças de Rotas", logContent, _routeLogFilePath, this);
                logForm.ShowDialog();
            }
            catch (Exception ex) { LogAppEvent($"Erro ao visualizar log de rotas: {ex.Message}", "ERROR"); MessageBox.Show($"Erro ao ler o log de rotas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ViewAppEventsLog()
        {
            try
            {
                if (!File.Exists(_appEventsLogFilePath)) { MessageBox.Show("Nenhum log de eventos encontrado.", "Log Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                string logContent = File.ReadAllText(_appEventsLogFilePath);
                if (string.IsNullOrWhiteSpace(logContent)) { MessageBox.Show("O log de eventos está vazio.", "Log Vazio", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                using var logForm = new LogDisplayForm("Log de Eventos da Aplicação", logContent, _appEventsLogFilePath, this);
                logForm.ShowDialog();
            }
            catch (Exception ex) { LogAppEvent($"Erro ao visualizar log de eventos: {ex.Message}", "ERROR"); MessageBox.Show($"Erro ao ler o log de eventos: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_hostMenuItemGoogle != null) _hostMenuItemGoogle.Checked = (pingTarget == "8.8.8.8");
            if (_hostMenuItemCloudflare != null) _hostMenuItemCloudflare.Checked = (pingTarget == "1.1.1.1");
            if (_hostMenuItemCustom != null) _hostMenuItemCustom.Checked = (pingTarget != "8.8.8.8" && pingTarget != "1.1.1.1");

            if (_clipboardMonitorMenuItem != null) _clipboardMonitorMenuItem.Checked = _clipboardMonitorEnabled;
            if (_networkSpeedMonitorMenuItem != null) _networkSpeedMonitorMenuItem.Checked = _networkSpeedMonitorEnabled;
            if (_autoCloseMenuItem_3s != null) _autoCloseMenuItem_3s.Checked = _defaultAutoCloseDuration == TimeSpan.FromSeconds(3);
            if (_autoCloseMenuItem_10s != null) _autoCloseMenuItem_10s.Checked = _defaultAutoCloseDuration == TimeSpan.FromSeconds(10);
            if (_autoCloseMenuItem_30s != null) _autoCloseMenuItem_30s.Checked = _defaultAutoCloseDuration == TimeSpan.FromSeconds(30);
            if (_autoCloseMenuItem_Never != null) _autoCloseMenuItem_Never.Checked = _defaultAutoCloseDuration == TimeSpan.Zero;
            if (_intervalMenuItem1Second != null) _intervalMenuItem1Second.Checked = (pingInterval == 1000);
            if (_intervalMenuItem3Seconds != null) _intervalMenuItem3Seconds.Checked = (pingInterval == 3000);
            if (_intervalMenuItem10Seconds != null) _intervalMenuItem10Seconds.Checked = (pingInterval == 10000);
            if (trayIcon.ContextMenuStrip?.Items.OfType<ToolStripMenuItem>().FirstOrDefault(item => item.Text == "Exibição do Ping (Ícone)") is ToolStripMenuItem displayModeItem)
            {
                foreach (ToolStripMenuItem subItem in displayModeItem.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (subItem.Text == "Último Ping") subItem.Checked = !_useAverageLatencyForIcon;
                    if (subItem.Text == $"Média dos Últimos {PING_HISTORY_COUNT_ICON} Pings") subItem.Checked = _useAverageLatencyForIcon;
                }
            }
            if (_icmpPingMenuItem != null) _icmpPingMenuItem.Checked = !_useTcpPing;
            if (_tcpPingMenuItem != null) _tcpPingMenuItem.Checked = _useTcpPing;
            if (_tcpPortMenuItem80 != null) _tcpPortMenuItem80.Checked = _useTcpPing && _tcpPingPort == 80;
            if (_tcpPortMenuItem443 != null) _tcpPortMenuItem443.Checked = _useTcpPing && _tcpPingPort == 443;
            if (_tcpPortMenuItemCustom != null) _tcpPortMenuItemCustom.Checked = _useTcpPing && _tcpPingPort != 80 && _tcpPingPort != 443;
            if (_tcpPingMenuItem != null && _tcpPingMenuItem.DropDownItems.Count > 0 && _tcpPingMenuItem.DropDownItems[0] is ToolStripMenuItem tcpPortSubMenu)
            {
                tcpPortSubMenu.Enabled = _useTcpPing;
            }

            if (_scanPortsMenuItem != null)
            {
                _scanPortsMenuItem.Enabled = IsValidHostnameOrIp(pingTarget) && !_isScanningPorts;
                if (_isScanningPorts)
                {
                    _scanPortsMenuItem.Text = "Escanear Portas (Escaneando...)";
                }
                else if (_lastScanOpenPorts.Any() && _lastScannedIpOrHost == pingTarget)
                {
                    _scanPortsMenuItem.Text = $"Escanear Portas ({_lastScanOpenPorts.Count} abertas)";
                }
                else
                {
                    _scanPortsMenuItem.Text = "Escanear Portas";
                }
            }


            if (_speedWindowDisplayModeMultiLineItem != null) _speedWindowDisplayModeMultiLineItem.Checked = !_speedWindowSingleLineMode;
            if (_speedWindowDisplayModeSingleLineItem != null) _speedWindowDisplayModeSingleLineItem.Checked = _speedWindowSingleLineMode;
            if (_speedWindowDisplayModeMenuItem != null) _speedWindowDisplayModeMenuItem.Enabled = _networkSpeedMonitorEnabled;


            if (_networkInterfaceMenuItem != null) UpdateNetworkInterfaceMenuItems();
            if (_dnsCheckMenuItem != null) _ = UpdateDnsMenuItemText();

            _ = UpdatePublicIpInfo(); 
            UpdatePrivateIpV4Info();  
            UpdatePrivateIpV6Info();  
            UpdateMacAddressMenuItemText();
        }

        private void SetPingInterval(int interval)
        {
            pingInterval = interval; pingTimer.Interval = interval;
            LogAppEvent($"Intervalo de ping: {interval}ms.");
            _ = UpdatePingLatencyAndJitter();
        }

        private void ResetPingState()
        {
            _latencyHistoryForIcon.Clear(); _rawLatencySamples.Clear();
            _currentLatencyJitterMs = -1; _lastLatencyValue = -1;
            _lastPingSuccess = false; _consecutiveFailedPings = 0;
            _wasPreviouslyFailingPings = false;
            UpdateIcon("...");
        }

        internal void SetPingTarget(string newHost)
        {
            string hostPart = newHost.Trim();
            if (!IsValidHostnameOrIp(hostPart)) { LogAppEvent($"Tentativa de definir alvo inválido: {hostPart}", "WARN"); MessageBox.Show($"Alvo '{hostPart}' não é um IP ou nome de host válido.", "Alvo Inválido", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            if (pingTarget == hostPart) return;
            pingTarget = hostPart;
            LogAppEvent($"Alvo de ping alterado para: {pingTarget}");
            ResetPingState();
            ClearRouteLogFile();
            trayIcon.Text = $"Atualizando alvo para {pingTarget}...";
            _lastScannedIpOrHost = "N/A";
            _lastScanOpenPorts.Clear();
            if (trayIcon.ContextMenuStrip != null) trayIcon.ContextMenuStrip.Invalidate(); 
            
            _ = UpdatePingLatencyAndJitter();
            tracertTimer.Enabled = _logRouteChangeEnabled;
            if (tracertTimer.Enabled) _ = CheckRouteChanges();
        }

        private void SetCustomPingTarget()
        {
            string? newHostInput = Interaction.InputBox("Digite o IP/Host para monitorar:", "Alvo personalizado", pingTarget);
            if (string.IsNullOrWhiteSpace(newHostInput)) return;
            newHostInput = newHostInput.Trim();
            if (!IsValidHostnameOrIp(newHostInput)) { LogAppEvent($"Tentativa de definir alvo personalizado inválido: {newHostInput}", "WARN"); MessageBox.Show($"Alvo '{newHostInput}' não parece ser um IP ou nome de host válido.", "Alvo Inválido", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            SetPingTarget(newHostInput);
        }

        private void UpdateIcon(string text)
        {
            Icon? previousIcon = trayIcon.Icon; Icon? newIcon = null;
            try { newIcon = CreateTextIcon(text, _lastPingSuccess, _lastLatencyValue); trayIcon.Icon = newIcon ?? _defaultIcon; }
            catch (Exception ex) { LogAppEvent($"Erro ao criar ícone: {ex.Message}", "ERROR"); trayIcon.Icon = _defaultIcon; newIcon = null; }
            finally
            {
                if (previousIcon != null && previousIcon != _defaultIcon && previousIcon != newIcon)
                {
                    try { if (previousIcon.Handle != IntPtr.Zero) DestroyIcon(previousIcon.Handle); } catch { }
                    previousIcon.Dispose();
                }
            }
        }

        private async Task UpdatePingLatencyAndJitter()
        {
            string pingTextForIconDisplay; string pingTooltipText; long currentLatency = -1;
            string currentTargetDisplay = pingTarget;
            string pingType = _useTcpPing ? $"TCP/{_tcpPingPort}" : "ICMP";

            if (!IsValidHostnameOrIp(pingTarget))
            {
                _lastLatencyValue = -1; _lastPingSuccess = false; _consecutiveFailedPings++;
                _latencyHistoryForIcon.Clear(); _rawLatencySamples.Clear(); _currentLatencyJitterMs = -1;
                pingTextForIconDisplay = _lastPingSuccess ? _lastLatencyValue.ToString() : "ERR";
                pingTooltipText = $"Alvo inválido: {currentTargetDisplay}";
                UpdateIcon(pingTextForIconDisplay); trayIcon.Text = pingTooltipText;
                if (!_wasPreviouslyFailingPings && _consecutiveFailedPings > 0) { trayIcon.ShowBalloonTip(3000, "Perda de Pacote/Falha no Ping", $"Alvo inválido: {currentTargetDisplay}", ToolTipIcon.Warning); LogAppEvent($"Início de falha (alvo inválido): {currentTargetDisplay}", "WARN"); _wasPreviouslyFailingPings = true; }
                UpdateFloatingSpeedWindowContent(); return;
            }
            try
            {
                if (_useTcpPing)
                {
                    currentLatency = await GetTcpPingLatency(pingTarget, _tcpPingPort);
                }
                else
                {
                    currentLatency = await GetIcmpPingLatency(pingTarget);
                }
                
                _lastPingSuccess = true;
                if (_wasPreviouslyFailingPings) { LogAppEvent($"Fim de perda de pacotes/falha para: {currentTargetDisplay} ({pingType})"); RecordPacketLossEvent(); _wasPreviouslyFailingPings = false; }
                _consecutiveFailedPings = 0;
                
                _pingStatistics.AddMeasurement(currentLatency);
                
                _alertSystem?.CheckLatency(currentLatency, true);
                
                if (_latencyHound != null && _latencyHound.Enabled && !_isLatencyHoundTracertRunning)
                {
                    if (_latencyHound.ShouldTriggerTracert(currentLatency))
                    {
                        _ = TriggerLatencyHoundTracert(currentLatency, currentTargetDisplay);
                    }
                    _latencyHound.AddLatencySample(currentLatency);
                }
                
                _latencyHistoryForIcon.Enqueue(currentLatency);
                if (_latencyHistoryForIcon.Count > PING_HISTORY_COUNT_ICON) _latencyHistoryForIcon.Dequeue();
                _lastLatencyValue = (_useAverageLatencyForIcon && _latencyHistoryForIcon.Any()) ? (long)_latencyHistoryForIcon.Average() : currentLatency;
                pingTextForIconDisplay = _lastPingSuccess ? _lastLatencyValue.ToString() : "ERR";
                string latencyDisplayForTooltip = (_useAverageLatencyForIcon && _latencyHistoryForIcon.Any()) ? $"{_latencyHistoryForIcon.Average():F0}ms (Avg)" : $"{currentLatency}ms (Last)";
                _rawLatencySamples.Enqueue(currentLatency);
                if (_rawLatencySamples.Count > RAW_LATENCY_SAMPLE_COUNT) _rawLatencySamples.Dequeue();
                _currentLatencyJitterMs = _rawLatencySamples.Count >= 2 ? _rawLatencySamples.Max() - _rawLatencySamples.Min() : -1;
                
                pingTooltipText = $"Latência {pingType} ({currentTargetDisplay}): {latencyDisplayForTooltip}";
            }
            catch (PingException ex)
            {
                _lastLatencyValue = -1; _lastPingSuccess = false; _consecutiveFailedPings++;
                _latencyHistoryForIcon.Clear(); _rawLatencySamples.Clear(); _currentLatencyJitterMs = -1;
                
                _pingStatistics.AddMeasurement(-1);
                
                _alertSystem?.CheckLatency(-1, false);
                
                pingTextForIconDisplay = "ERR";
                pingTooltipText = $"Falha no {pingType} para {currentTargetDisplay}{(ex.InnerException != null ? $"\nDetalhe: {ex.InnerException.Message.Split('.')[0]}" : "")}";
                if (!_wasPreviouslyFailingPings && _consecutiveFailedPings > 0) { trayIcon.ShowBalloonTip(3000, "Perda de Pacote/Falha no Ping", $"Falha no {pingType} para {currentTargetDisplay}.", ToolTipIcon.Warning); LogAppEvent($"Início de perda de pacotes/falha para: {currentTargetDisplay} ({pingType}). Erro: {ex.Message.Split('.')[0]}", "WARN"); _wasPreviouslyFailingPings = true; }
            }
            catch (Exception ex)
            {
                _lastLatencyValue = -1; _lastPingSuccess = false; _consecutiveFailedPings++;
                _latencyHistoryForIcon.Clear(); _rawLatencySamples.Clear(); _currentLatencyJitterMs = -1;
                
                _pingStatistics.AddMeasurement(-1);
                
                _alertSystem?.CheckLatency(-1, false);
                
                pingTextForIconDisplay = "ERR";
                pingTooltipText = $"Erro ao resolver/pingar {currentTargetDisplay} ({pingType}): {ex.Message.Split('.')[0]}";
                if (!_wasPreviouslyFailingPings && _consecutiveFailedPings > 0) { trayIcon.ShowBalloonTip(3000, "Perda de Pacote/Falha no Ping", $"Erro ao pingar {currentTargetDisplay} ({pingType}).", ToolTipIcon.Warning); LogAppEvent($"Erro ao resolver/pingar {currentTargetDisplay} ({pingType}): {ex.Message.Split('.')[0]}", "ERROR"); _wasPreviouslyFailingPings = true; }
            }
            UpdateIcon(pingTextForIconDisplay);
            trayIcon.Text = pingTooltipText.Length > 63 ? pingTooltipText.Substring(0, 60) + "..." : pingTooltipText;
            if (_networkSpeedMonitorEnabled && _floatingSpeedWindow != null && !_floatingSpeedWindow.IsDisposed) UpdateFloatingSpeedWindowContent();
        }

        private void UpdateNetworkSpeedAndWindow()
        {
            if (!_networkSpeedMonitorEnabled || _selectedNetworkInterface == null) { _currentUploadSpeed = 0; _currentDownloadSpeed = 0; CloseFloatingSpeedWindow(); return; }
            try
            {
                if (_uploadCounter == null || _downloadCounter == null) { InitializeNetworkSpeedCounters(); if (_uploadCounter == null || _downloadCounter == null) { SetNetworkSpeedMonitorEnabled(false); return; } }
                _currentUploadSpeed = _uploadCounter!.NextValue(); _currentDownloadSpeed = _downloadCounter!.NextValue();
                UpdateFloatingSpeedWindowContent();
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                LogAppEvent($"Erro ao ler contadores de desempenho: {ex.Message}", "ERROR"); SetNetworkSpeedMonitorEnabled(false);
                string msg = ex is InvalidOperationException ? $"Erro ao acessar contadores para '{_selectedNetworkInterface?.Description}'. Interface desconectada? Desativando monitor." : $"Permissão negada. Execute como administrador.";
                MessageBox.Show(msg, "Erro ao Monitorar Rede", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex) { LogAppEvent($"Erro inesperado no monitor de rede: {ex.Message}", "ERROR"); SetNetworkSpeedMonitorEnabled(false); MessageBox.Show($"Erro inesperado: {ex.Message}. Desativando monitor.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void InitializeNetworkSpeedCounters()
        {
            _uploadCounter?.Dispose(); _downloadCounter?.Dispose(); _uploadCounter = null; _downloadCounter = null;
            if (_selectedNetworkInterface == null) return;
            try
            {
                string originalInstanceName = _selectedNetworkInterface.Description.Replace("(", "[").Replace(")", "]").Replace("#", "_");
                var category = new PerformanceCounterCategory("Network Interface");
                var instanceNames = category.GetInstanceNames();
                string instanceNameToUse = originalInstanceName;
                if (!instanceNames.Contains(originalInstanceName))
                {
                    string shortDesc = _selectedNetworkInterface.Description.Substring(0, Math.Min(_selectedNetworkInterface.Description.Length, 10));
                    instanceNameToUse = instanceNames.FirstOrDefault(i => i.Contains(shortDesc)) ?? originalInstanceName;
                    if (!instanceNames.Contains(instanceNameToUse)) { LogAppEvent($"Instância '{instanceNameToUse}' (de '{originalInstanceName}') não encontrada.", "WARN"); return; }
                }
                _uploadCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instanceNameToUse, true);
                _downloadCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", instanceNameToUse, true);
                _uploadCounter.NextValue(); _downloadCounter.NextValue();
            }
            catch (Exception ex) { LogAppEvent($"Falha ao inicializar contadores para '{_selectedNetworkInterface.Description}': {ex.Message}", "ERROR"); MessageBox.Show($"Não foi possível inicializar contadores para '{_selectedNetworkInterface.Description}'.\n{ex.Message}", "Erro Contador", MessageBoxButtons.OK, MessageBoxIcon.Warning); SetNetworkSpeedMonitorEnabled(false); }
        }

        private void SetNetworkSpeedMonitorEnabled(bool enabled)
        {
            _networkSpeedMonitorEnabled = enabled;
            LogAppEvent($"Monitor de velocidade {(_networkSpeedMonitorEnabled ? "ATIVADO" : "DESATIVADO")}.");
            networkSpeedTimer.Enabled = _networkSpeedMonitorEnabled;
            if (_networkSpeedMonitorEnabled)
            {
                if (_selectedNetworkInterface == null)
                {
                    _selectedNetworkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet);
                    if (_selectedNetworkInterface == null) { _networkSpeedMonitorEnabled = false; networkSpeedTimer.Enabled = false; if (_networkSpeedMonitorMenuItem != null) _networkSpeedMonitorMenuItem.Checked = false; LogAppEvent("Nenhuma interface válida para monitorar.", "WARN"); MessageBox.Show("Nenhuma interface válida.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
                    LogAppEvent($"Interface selecionada: {_selectedNetworkInterface.Description}");
                }
                InitializeNetworkSpeedCounters();
                if (_uploadCounter == null || _downloadCounter == null) { _networkSpeedMonitorEnabled = false; networkSpeedTimer.Enabled = false; if (_networkSpeedMonitorMenuItem != null) _networkSpeedMonitorMenuItem.Checked = false; return; }
                CreateFloatingSpeedWindow();
            }
            else { _currentUploadSpeed = 0; _currentDownloadSpeed = 0; _uploadCounter?.Dispose(); _downloadCounter?.Dispose(); _uploadCounter = null; _downloadCounter = null; CloseFloatingSpeedWindow(); }
            if (_networkSpeedMonitorMenuItem != null) _networkSpeedMonitorMenuItem.Checked = _networkSpeedMonitorEnabled;
        }

        private void CreateFloatingSpeedWindow()
        {
            if (_floatingSpeedWindow == null || _floatingSpeedWindow.IsDisposed)
            {
                _floatingSpeedWindow = new FloatingSpeedWindow();
                _floatingSpeedWindow.FormClosed += (s, e) => _floatingSpeedWindow = null;
                _floatingSpeedWindow.SetDisplayMode(_speedWindowSingleLineMode);
                _floatingSpeedWindow.Show();
                UpdateFloatingSpeedWindowContent();
            }
            else
            {
                _floatingSpeedWindow.SetDisplayMode(_speedWindowSingleLineMode);
                if (!_floatingSpeedWindow.Visible) _floatingSpeedWindow.Show();
                UpdateFloatingSpeedWindowContent();
            }
        }
        private void CloseFloatingSpeedWindow() => _floatingSpeedWindow?.Close();
        private void UpdateFloatingSpeedWindowContent() { if (_floatingSpeedWindow == null || _floatingSpeedWindow.IsDisposed) return; _floatingSpeedWindow.SetSpeedText(FormatSpeed(_currentUploadSpeed, "↑"), FormatSpeed(_currentDownloadSpeed, "↓"), $"Jitter: {(_currentLatencyJitterMs >= 0 ? _currentLatencyJitterMs + "ms" : "N/A")}"); }

        private void UpdateNetworkInterfaceMenuItems()
        {
            if (_networkInterfaceMenuItem == null) return;
            _networkInterfaceMenuItem.DropDownItems.Clear();
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces().Where(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet or NetworkInterfaceType.Ppp).ToList();
            if (!networkInterfaces.Any()) { _networkInterfaceMenuItem.Enabled = false; _networkInterfaceMenuItem.DropDownItems.Add(new ToolStripMenuItem("Nenhuma interface ativa") { Enabled = false }); }
            else
            {
                _networkInterfaceMenuItem.Enabled = true;
                foreach (var ni in networkInterfaces) { var item = new ToolStripMenuItem(ni.Description, null, (s, e) => SelectNetworkInterface(ni)) { Checked = _selectedNetworkInterface?.Id == ni.Id }; _networkInterfaceMenuItem.DropDownItems.Add(item); }
            }
        }

        private void SelectNetworkInterface(NetworkInterface ni)
        {
            if (_selectedNetworkInterface?.Id == ni.Id) return;
            _selectedNetworkInterface = ni;
            LogAppEvent($"Interface de rede para monitor: {ni.Description}");
            InitializeNetworkSpeedCounters(); _currentUploadSpeed = 0; _currentDownloadSpeed = 0;
            UpdateFloatingSpeedWindowContent();
            if (_networkSpeedMonitorEnabled) { networkSpeedTimer.Stop(); networkSpeedTimer.Start(); UpdateNetworkSpeedAndWindow(); }
        }

        private string FormatSpeed(double bytesPerSecond, string prefix)
        {
            bytesPerSecond = Math.Max(0, bytesPerSecond); string[] sizes = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" }; int order = 0; double speed = bytesPerSecond;
            while (speed >= 1024 && order < sizes.Length - 1) { order++; speed /= 1024; }
            string format = (order == 0) ? "F0" : (speed < 10 ? "F2" : (speed < 100 ? "F1" : "F0"));
            return $"{prefix} {speed.ToString(format, CultureInfo.InvariantCulture)}{sizes[order]}";
        }

        private async Task<long> GetIcmpPingLatency(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host não pode ser nulo.", nameof(host));
            using var pingSender = new Ping();
            try
            {
                IPAddress[] addresses = await _dnsCache.GetHostAddressesAsync(host);
                if (addresses.Length == 0) throw new PingException($"Host não resolvido: {host}.");
                PingReply reply = await pingSender.SendPingAsync(addresses[0], 2000);
                return reply.Status switch
                {
                    IPStatus.Success => Math.Max(0, reply.RoundtripTime),
                    IPStatus.TimedOut => throw new PingException($"Timeout: {host} ({addresses[0]})."),
                    _ => throw new PingException($"Ping failed: {host} ({addresses[0]}). Status: {reply.Status}"),
                };
            }
            catch (SocketException sex) { LogAppEvent($"Falha DNS para {host}: {sex.Message}", "ERROR"); throw new PingException($"DNS failed {host}: {sex.Message}", sex); }
            catch (PingException) { throw; }
            catch (Exception ex) { LogAppEvent($"Erro ICMP para {host}: {ex.Message}", "ERROR"); throw new PingException($"ICMP Ping error {host}: {ex.Message}", ex); }
        }

        private async Task<long> GetTcpPingLatency(string host, int port)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("Host não pode ser nulo.", nameof(host));
            if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port), "Porta deve estar entre 1 e 65535.");

            try
            {
                IPAddress[] addresses = await _dnsCache.GetHostAddressesAsync(host);
                if (addresses.Length == 0) throw new PingException($"Host não resolvido: {host}.");
                
                var stopwatch = Stopwatch.StartNew();
                using (var client = new TcpClient())
                {
                    var connectTask = client.ConnectAsync(addresses[0], port);
                    var timeoutTask = Task.Delay(2000);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        throw new PingException($"Timeout ({2000}ms) ao conectar a {host}:{port}");
                    }

                    await connectTask; 
                }
                stopwatch.Stop();
                return Math.Max(0, stopwatch.ElapsedMilliseconds);
            }
            catch (SocketException sex)
            {
                LogAppEvent($"Falha TCP para {host}:{port}: {sex.Message}", "ERROR");
                throw new PingException($"TCP Connect failed to {host}:{port}: {sex.Message.Split(':')[0]}", sex);
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro TCP para {host}:{port}: {ex.Message}", "ERROR");
                throw new PingException($"TCP Ping error to {host}:{port}: {ex.Message}", ex);
            }
        }

        private async void ScanPortsMenuItem_DropDownOpening(object? sender, EventArgs e)
        {
            await Task.Yield(); 

            if (_scanPortsMenuItem == null) return;

            _scanPortsMenuItem.DropDownItems.Clear();

            var scanNowItem = new ToolStripMenuItem("Escanear Agora");
            scanNowItem.Click += async (s, eArgs) => {
                _scanPortsMenuItem.DropDown.Close();
                await PerformPortScanAndDisplayResults();
            };
            _scanPortsMenuItem.DropDownItems.Add(scanNowItem);
            _scanPortsMenuItem.DropDownItems.Add(new ToolStripSeparator());

            if (_isScanningPorts)
            {
                _scanPortsMenuItem.DropDownItems.Add(new ToolStripLabel("Escaneando...") { ForeColor = Color.Yellow });
            }
            else if (_lastScannedIpOrHost == pingTarget && _lastScanOpenPorts.Any())
            {
                _scanPortsMenuItem.DropDownItems.Add(new ToolStripLabel($"Portas abertas em {pingTarget}:") { ForeColor = Color.LightBlue, Font = new Font(_scanPortsMenuItem.Font, FontStyle.Bold) });
                foreach (var port in _lastScanOpenPorts.OrderBy(p => p))
                {
                    string portName = _commonTcpPortsToScan.TryGetValue(port, out string? name) ? $" ({name})" : "";
                    var item = new ToolStripMenuItem($"{port}{portName}");
                    item.Tag = port;
                    item.Click += (s, eArgs) =>
                    {
                        if (s is ToolStripMenuItem clickedItem && clickedItem.Tag is int selectedPort)
                        {
                            SetTcpPingPortAndDisplay(selectedPort);
                            _scanPortsMenuItem.DropDown.Close();
                        }
                    };
                    _scanPortsMenuItem.DropDownItems.Add(item);
                }
            }
            else if (_lastScannedIpOrHost == pingTarget && !_lastScanOpenPorts.Any())
            {
                    _scanPortsMenuItem.DropDownItems.Add(new ToolStripLabel($"Nenhuma porta aberta em {pingTarget}.") { ForeColor = Color.Red });
            }
            else
            {
                _scanPortsMenuItem.DropDownItems.Add(new ToolStripLabel("Nenhum escaneamento recente ou alvo diferente.") { ForeColor = Color.Gray });
            }
        }


        private async Task PerformPortScanAndDisplayResults()
        {
            if (!IsValidHostnameOrIp(pingTarget) || _isScanningPorts)
            {
                if (_isScanningPorts)
                {
                    trayIcon.ShowBalloonTip(1500, "Escaneamento em Andamento", "Um escaneamento já está em progresso.", ToolTipIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"O alvo '{pingTarget}' não é um IP ou nome de host válido para escanear portas.", "Alvo Inválido", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    LogAppEvent($"Tentativa de escanear portas para alvo inválido: {pingTarget}", "WARN");
                }
                return;
            }

            _isScanningPorts = true;
            if (_scanPortsMenuItem != null)
            {
                _scanPortsMenuItem.Enabled = false;
                _scanPortsMenuItem.Text = "Escanear Portas (Escaneando...)";
            }

            trayIcon.ShowBalloonTip(1500, "Escaneando Portas", $"Escaneando portas comuns em {pingTarget}...", ToolTipIcon.Info);
            LogAppEvent($"Iniciando escaneamento de portas para {pingTarget}...");

            try
            {
                _lastScannedIpOrHost = pingTarget;
                _lastScanOpenPorts = await PerformPortScan(pingTarget, _commonTcpPortsToScan.Keys.ToArray());

                if (_lastScanOpenPorts.Any())
                {
                    trayIcon.ShowBalloonTip(3000, "Escaneamento Concluído", $"Encontradas {_lastScanOpenPorts.Count} portas abertas em {pingTarget}.", ToolTipIcon.Info);
                    LogAppEvent($"Escaneamento de portas para {pingTarget} concluído. Portas abertas: {string.Join(", ", _lastScanOpenPorts.OrderBy(p => p))}", "INFO");
                }
                else
                {
                    trayIcon.ShowBalloonTip(2000, "Escaneamento Concluído", $"Nenhuma porta aberta em {pingTarget}.", ToolTipIcon.Info);
                    LogAppEvent($"Escaneamento de portas para {pingTarget} concluído. Nenhuma porta comum aberta.", "INFO");
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"Erro ao escanear portas para {pingTarget}: {ex.Message.Split('.')[0]}";
                MessageBox.Show(errorMsg, "Erro no Escaneamento de Portas", MessageBoxButtons.OK, MessageBoxIcon.Error);
                LogAppEvent(errorMsg, "ERROR");
                _lastScannedIpOrHost = "N/A";
                _lastScanOpenPorts.Clear();
            }
            finally
            {
                _isScanningPorts = false;
                if (_scanPortsMenuItem != null)
                {
                    _scanPortsMenuItem.Enabled = true;
                    if (_lastScanOpenPorts.Any() && _lastScannedIpOrHost == pingTarget)
                    {
                        _scanPortsMenuItem.Text = $"Escanear Portas ({_lastScanOpenPorts.Count} abertas)";
                    }
                    else
                    {
                        _scanPortsMenuItem.Text = "Escanear Portas";
                    }
                }
            }
        }


        private async Task<List<int>> PerformPortScan(string targetHost, int[] portsToScan)
        {
            List<int> openPorts = new();
            IPAddress? targetIpAddress = null;
            try
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(targetHost);
                targetIpAddress = addresses.FirstOrDefault();
                if (targetIpAddress == null)
                {
                    throw new Exception($"Não foi possível resolver o host: {targetHost}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Erro de resolução de DNS para {targetHost}: {ex.Message}", ex);
            }

            const int maxConcurrency = 50; 
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task>();

            foreach (var port in portsToScan)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (var client = new TcpClient())
                        {
                            var connectTask = client.ConnectAsync(targetIpAddress!, port);
                            var timeoutTask = Task.Delay(1000);

                            var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                            if (completedTask == connectTask && connectTask.Status == TaskStatus.RanToCompletion)
                            {
                                lock (openPorts)
                                {
                                    openPorts.Add(port);
                                }
                            }
                        }
                    }
                    catch (Exception ex_port)
                    {
                        Debug.WriteLine($"Erro ao escanear porta {port} em {targetHost}: {ex_port.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);

            return openPorts;
        }

        private async Task ScanLocalPortsAsync()
        {
            if (_isScanningLocalPorts || _localPortsMenuItem == null) return;

            _isScanningLocalPorts = true;
            Action updateStart = () => 
            {
                 if (_localPortsMenuItem != null) _localPortsMenuItem.Text = "Portas Locais: Checando...";
            };
            if (trayIcon.ContextMenuStrip?.InvokeRequired ?? false) trayIcon.ContextMenuStrip.Invoke(updateStart); else updateStart();

            LogAppEvent("Iniciando verificação de portas abertas (Listening)...");

            try
            {
                _lastActiveConnections = await Task.Run(() =>
                {
                    var properties = IPGlobalProperties.GetIPGlobalProperties();
                    
                    var listeners = properties.GetActiveTcpListeners();
                    
                    var result = new List<string>();
                    foreach (var listener in listeners)
                    {
                         string local = $"{listener.Address}:{listener.Port}";
                         string portName = _commonTcpPortsToScan.TryGetValue(listener.Port, out string? name) ? $" ({name})" : "";
                         
                         result.Add($"{local}{portName}");
                    }
                    
                    return result.OrderBy(x => x).ToList();
                });
                
                LogAppEvent($"Verificação local concluída. Portas abertas (Listening): {_lastActiveConnections.Count}.");
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao verificar conexões locais: {ex.Message}", "ERROR");
                _lastActiveConnections.Clear();
            }
            finally
            {
                _isScanningLocalPorts = false;
                Action updateEnd = () => 
                {
                    if (_localPortsMenuItem != null) 
                    {
                        _localPortsMenuItem.Text = $"Portas Locais ({_lastActiveConnections.Count} ativas)";
                    }
                };
                 if (trayIcon.ContextMenuStrip?.InvokeRequired ?? false) trayIcon.ContextMenuStrip.Invoke(updateEnd); else updateEnd();
            }
        }

        private void LocalPortsMenuItem_DropDownOpening(object? sender, EventArgs e)
        {
            if (_localPortsMenuItem == null) return;
            _localPortsMenuItem.DropDownItems.Clear();

            var rescanItem = new ToolStripMenuItem("Reescanear Agora");
            rescanItem.Click += async (s, args) => 
            {
                _localPortsMenuItem.DropDown.Close();
                await ScanLocalPortsAsync();
            };
            _localPortsMenuItem.DropDownItems.Add(rescanItem);
            _localPortsMenuItem.DropDownItems.Add(new ToolStripSeparator());

            if (_isScanningLocalPorts)
            {
                 _localPortsMenuItem.DropDownItems.Add(new ToolStripLabel("Escaneando...") { ForeColor = Color.Yellow });
            }
            else if (_lastActiveConnections.Any())
            {
                var displayList = _lastActiveConnections.Take(50).ToList();
                
                foreach (var connString in displayList)
                {
                    _localPortsMenuItem.DropDownItems.Add(new ToolStripMenuItem(connString));
                }
                
                if (_lastActiveConnections.Count > 50)
                {
                     _localPortsMenuItem.DropDownItems.Add(new ToolStripSeparator());
                     _localPortsMenuItem.DropDownItems.Add(new ToolStripMenuItem($"E mais {_lastActiveConnections.Count - 50} conexões...") { Enabled = false });
                }
            }
            else
            {
                _localPortsMenuItem.DropDownItems.Add(new ToolStripLabel("Nenhuma conexão ativa encontrada.") { ForeColor = Color.Gray });
            }
        }


        private Icon CreateTextIcon(string text, bool success, long latencyValue)
        {
            const int bitmapSize = 16; Color textColor = Color.White; Color bgColor = !success ? (latencyValue < 0 ? Color.DarkBlue : Color.DarkRed) : (latencyValue < 39 ? Color.Green : (latencyValue < 45 ? Color.Orange : Color.Red));
            using var bitmap = new Bitmap(bitmapSize, bitmapSize); using var g = Graphics.FromImage(bitmap); g.Clear(bgColor);
            if (!string.IsNullOrEmpty(text))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.Half; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                string displayText = (success && latencyValue >= 0) ? (latencyValue > 999 ? "99+" : (latencyValue > 99 ? latencyValue.ToString() : latencyValue.ToString(latencyValue > 9 ? "D2" : "D1"))) : (text.Equals("ERR", StringComparison.OrdinalIgnoreCase) ? "ER" : "?");
                Font? tempFont = null;
                
                if (displayText.Length > 2) tempFont = new Font(_fixedFont.FontFamily, 7.0f, _fixedFont.Style);
                else if (displayText.Length == 2) tempFont = new Font(_fixedFont.FontFamily, 9f, _fixedFont.Style);
                Font actualFont = tempFont ?? _fixedFont;
                using (tempFont) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip, Trimming = StringTrimming.None }) using (var textBrush = new SolidBrush(textColor)) { g.DrawString(displayText, actualFont, textBrush, new RectangleF(0, (displayText.Length <= 1 ? 0.5f : 0), bitmap.Width, bitmap.Height - (displayText.Length <= 1 ? 0.5f : 0)), sf); }
            }
            IntPtr hIcon = IntPtr.Zero; Icon? newIcon = null; Icon? clonedIcon = null;
            try { hIcon = bitmap.GetHicon(); if (hIcon != IntPtr.Zero) { newIcon = Icon.FromHandle(hIcon); clonedIcon = (Icon)newIcon.Clone(); } }
            catch { return _defaultIcon ?? SystemIcons.Application; }
            finally { if (hIcon != IntPtr.Zero) DestroyIcon(hIcon); newIcon?.Dispose(); }
            return clonedIcon ?? _defaultIcon ?? SystemIcons.Application;
        }

        private void ClearRouteLogFile() { try { if (File.Exists(_routeLogFilePath)) File.WriteAllText(_routeLogFilePath, string.Empty); LogAppEvent("Log de rotas limpo."); } catch (Exception ex) { LogAppEvent($"Erro ao limpar log de rotas: {ex.Message}", "ERROR"); MessageBox.Show($"Erro ao limpar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); } }

        private async Task CheckRouteChanges()
        {
            if (!_logRouteChangeEnabled) return;
            if (!IsValidHostnameOrIp(pingTarget)) { _lastRoute = "Alvo inválido para tracert."; return; }
            string currentRoute = await RunTraceroute(pingTarget);
            if (string.IsNullOrEmpty(currentRoute) || currentRoute == _lastRoute || currentRoute.StartsWith("Erro")) return;
            if (!string.IsNullOrEmpty(_lastRoute) && _lastRoute != "Alvo inválido para tracert." && !_lastRoute.StartsWith("Erro")) { trayIcon.ShowBalloonTip(5000, "Rota de Rede Alterada", $"A rota para {pingTarget} mudou.", ToolTipIcon.Info); LogRouteChange(_lastRoute, currentRoute); LogAppEvent($"Mudança de rota para {pingTarget}. Nova: {currentRoute.Replace(Environment.NewLine, " ")}"); }
            _lastRoute = currentRoute;
        }

        private void LogRouteChange(string oldRoute, string newRoute) { string logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\nRota alterada de:\n{FormatRoute(oldRoute)}\npara:\n{FormatRoute(newRoute)}\n\n"; try { File.AppendAllText(_routeLogFilePath, logEntry); } catch (Exception ex) { LogAppEvent($"Erro ao escrever no log de rotas: {ex.Message}", "ERROR"); MessageBox.Show($"Error writing to log: {ex.Message}", "Erro de Log", MessageBoxButtons.OK, MessageBoxIcon.Error); } }
        private string FormatRoute(string route) => string.IsNullOrEmpty(route) ? "(Rota desconhecida)" : route.Replace(" > ", "\n  > ");

        private async Task<string> RunTraceroute(string host)
        {
            try
            {
                using var process = new Process { StartInfo = new ProcessStartInfo("tracert", $"-d -h 15 -w 500 {host}") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true } };
                process.Start(); string output = await process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(10000)) { try { process.Kill(true); } catch { } return "Erro: tracert demorou."; }
                return process.ExitCode == 0 ? ParseTracertOutput(output) : $"Erro tracert (código {process.ExitCode}).";
            }
            catch (Win32Exception wEx) { LogAppEvent($"Win32Exception tracert {host}: {wEx.Message}", "ERROR"); return "Erro tracert (Win32Exception)."; }
            catch (Exception ex) { LogAppEvent($"Erro tracert {host}: {ex.Message}", "ERROR"); return "Erro tracert: " + ex.Message; }
        }

        private string ParseTracertOutput(string output)
        {
            var parsedRoute = new StringBuilder(); var ipRegex = new Regex(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b"); bool traceStarted = false;
            foreach (string line in output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("Tracing route to", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("Rastreando a rota para", StringComparison.OrdinalIgnoreCase)) { traceStarted = true; continue; }
                if (!traceStarted || trimmedLine.StartsWith("Trace complete", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("Rastreamento concluído", StringComparison.OrdinalIgnoreCase)) break;
                if (Regex.IsMatch(trimmedLine, @"^\s*\d+")) { string hopIp = ipRegex.Matches(trimmedLine).Cast<Match>().LastOrDefault()?.Value ?? "N/A"; if (parsedRoute.Length > 0) parsedRoute.Append(" > "); parsedRoute.Append(hopIp); }
            }
            return parsedRoute.Length > 0 ? parsedRoute.ToString() : "Rota não determinada";
        }

        private void ShowStatistics()
        {
            try
            {
                using var form = new Form
                {
                    Text = "Estatísticas de Ping",
                    ClientSize = new Size(560, 680),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterScreen,
                    MaximizeBox = false,
                    MinimizeBox = false,
                    BackColor = Color.FromArgb(30, 30, 35)
                };

                var headerPanel = new Panel
                {
                    Size = new Size(560, 90),
                    Location = new Point(0, 0),
                    BackColor = Color.FromArgb(45, 45, 55)
                };

                var headerLabel = new Label
                {
                    Text = "Estatísticas de Ping",
                    Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                    ForeColor = Color.White,
                    Location = new Point(20, 15),
                    AutoSize = true
                };

                var targetLabel = new Label
                {
                    Text = $"Alvo: {pingTarget}",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(150, 150, 160),
                    Location = new Point(22, 45),
                    AutoSize = true
                };

                var uptime = _pingStatistics.SessionUptime;
                var uptimeLabel = new Label
                {
                    Text = $"Sessão: {uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(150, 150, 160),
                    Location = new Point(22, 65),
                    AutoSize = true
                };

                int qualityScore = _pingStatistics.QualityScore;
                string qualityRating = _pingStatistics.QualityRating;
                Color qualityColor = qualityScore >= 90 ? Color.FromArgb(40, 167, 69) :
                                     qualityScore >= 75 ? Color.FromArgb(0, 180, 255) :
                                     qualityScore >= 50 ? Color.FromArgb(255, 193, 7) :
                                     qualityScore >= 25 ? Color.FromArgb(255, 128, 0) :
                                     Color.FromArgb(220, 53, 69);

                var qualityPanel = new Panel
                {
                    Size = new Size(130, 80),
                    Location = new Point(415, 5),
                    BackColor = Color.Transparent
                };

                qualityPanel.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    using var bgPen = new Pen(Color.FromArgb(60, 60, 70), 8);
                    g.DrawArc(bgPen, 15, 10, 60, 60, 135, 270);
                    
                    float sweepAngle = (qualityScore / 100f) * 270;
                    using var fillPen = new Pen(qualityColor, 8);
                    g.DrawArc(fillPen, 15, 10, 60, 60, 135, sweepAngle);
                    
                    using var scoreFont = new Font("Segoe UI", 14f, FontStyle.Bold);
                    using var scoreBrush = new SolidBrush(Color.White);
                    var scoreText = qualityScore.ToString();
                    var scoreSize = g.MeasureString(scoreText, scoreFont);
                    g.DrawString(scoreText, scoreFont, scoreBrush, 45 - scoreSize.Width / 2, 28);
                    
                    using var ratingFont = new Font("Segoe UI", 8f, FontStyle.Bold);
                    using var ratingBrush = new SolidBrush(qualityColor);
                    var ratingSize = g.MeasureString(qualityRating, ratingFont);
                    g.DrawString(qualityRating, ratingFont, ratingBrush, 45 - ratingSize.Width / 2, 72);
                };

                headerPanel.Controls.AddRange(new Control[] { headerLabel, targetLabel, uptimeLabel, qualityPanel });

                var cardsPanel = new Panel
                {
                    Size = new Size(530, 130),
                    Location = new Point(15, 100),
                    BackColor = Color.Transparent
                };

                int cardWidth = 168;
                int cardHeight = 58;
                int cardSpacing = 8;

                double successRate = _pingStatistics.TotalPings > 0 ? ((_pingStatistics.SuccessfulPings * 100.0) / _pingStatistics.TotalPings) : 0;
                Color successColor = successRate >= 99 ? Color.FromArgb(40, 167, 69) : successRate >= 95 ? Color.FromArgb(255, 193, 7) : Color.FromArgb(220, 53, 69);
                
                var totalCard = CreateStatCard("Total Pings", $"{_pingStatistics.TotalPings}", Color.FromArgb(0, 150, 199), new Point(0, 0), cardWidth, cardHeight);
                var successCard = CreateStatCard("Taxa Sucesso", $"{successRate:F1}%", successColor, new Point(cardWidth + cardSpacing, 0), cardWidth, cardHeight);
                
                Color lossColor = _pingStatistics.PacketLossPercentage <= 1 ? Color.FromArgb(40, 167, 69) : _pingStatistics.PacketLossPercentage <= 5 ? Color.FromArgb(255, 193, 7) : Color.FromArgb(220, 53, 69);
                var lossCard = CreateStatCard("Perda Pacotes", $"{_pingStatistics.PacketLossPercentage:F1}%", lossColor, new Point((cardWidth + cardSpacing) * 2, 0), cardWidth, cardHeight);

                var stdCard = CreateStatCard("Desvio Padrão", $"{_pingStatistics.StandardDeviation:F1}ms", Color.FromArgb(111, 66, 193), new Point(0, cardHeight + cardSpacing), cardWidth, cardHeight);
                
                Color jitterColor = _pingStatistics.Jitter <= 5 ? Color.FromArgb(40, 167, 69) : _pingStatistics.Jitter <= 15 ? Color.FromArgb(255, 193, 7) : Color.FromArgb(220, 53, 69);
                var jitterCard = CreateStatCard("Jitter", $"{_pingStatistics.Jitter:F1}ms", jitterColor, new Point(cardWidth + cardSpacing, cardHeight + cardSpacing), cardWidth, cardHeight);
                
                long lastLat = _pingStatistics.LastLatency;
                string lastLatText = lastLat >= 0 ? $"{lastLat}ms" : "N/A";
                Color lastLatColor = lastLat < 0 ? Color.FromArgb(100, 100, 110) : lastLat <= 30 ? Color.FromArgb(40, 167, 69) : lastLat <= 80 ? Color.FromArgb(255, 193, 7) : Color.FromArgb(220, 53, 69);
                var lastLatCard = CreateStatCard("Último Ping", lastLatText, lastLatColor, new Point((cardWidth + cardSpacing) * 2, cardHeight + cardSpacing), cardWidth, cardHeight);

                cardsPanel.Controls.AddRange(new Control[] { totalCard, successCard, lossCard, stdCard, jitterCard, lastLatCard });

                var graphPanel = new Panel
                {
                    Size = new Size(530, 170),
                    Location = new Point(15, 240),
                    BackColor = Color.FromArgb(40, 40, 48)
                };

                var graphTitle = new Label
                {
                    Text = "HISTÓRICO DE LATÊNCIA",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(150, 150, 160),
                    Location = new Point(15, 8),
                    AutoSize = true
                };

                var graphCanvas = new Panel
                {
                    Size = new Size(500, 130),
                    Location = new Point(15, 32),
                    BackColor = Color.FromArgb(25, 25, 30)
                };

                var history = _pingStatistics.GetHistory().ToArray();
                long minLatGraph = history.Length > 0 ? history.Min() : 0;
                long maxLatGraph = history.Length > 0 ? history.Max() : 100;
                double avgLatGraph = history.Length > 0 ? history.Average() : 0;

                graphCanvas.Paint += (s, e) =>
                {
                    var g = e.Graphics;
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    
                    int w = graphCanvas.Width;
                    int h = graphCanvas.Height;
                    int padding = 5;
                    int graphH = h - padding * 2;
                    int graphW = w - padding * 2;
                    
                    using var gridPen = new Pen(Color.FromArgb(50, 50, 60), 1);
                    for (int i = 0; i <= 4; i++)
                    {
                        int y = padding + (graphH * i / 4);
                        g.DrawLine(gridPen, padding, y, w - padding, y);
                    }

                    if (history.Length < 2)
                    {
                        using var noDataFont = new Font("Segoe UI", 10f);
                        using var noDataBrush = new SolidBrush(Color.FromArgb(100, 100, 110));
                        string noDataText = "Aguardando dados...";
                        var textSize = g.MeasureString(noDataText, noDataFont);
                        g.DrawString(noDataText, noDataFont, noDataBrush, (w - textSize.Width) / 2, (h - textSize.Height) / 2);
                        return;
                    }

                    long range = Math.Max(maxLatGraph - minLatGraph, 10);
                    long scaleMin = Math.Max(0, minLatGraph - range / 10);
                    long scaleMax = maxLatGraph + range / 10;
                    long scaleRange = Math.Max(scaleMax - scaleMin, 1);

                    int avgY = padding + (int)((1 - (avgLatGraph - scaleMin) / scaleRange) * graphH);
                    using var avgPen = new Pen(Color.FromArgb(100, 0, 123, 255), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                    g.DrawLine(avgPen, padding, avgY, w - padding, avgY);

                    using var scaleFont = new Font("Segoe UI", 7f);
                    using var scaleBrush = new SolidBrush(Color.FromArgb(100, 100, 110));
                    g.DrawString($"{scaleMax}ms", scaleFont, scaleBrush, w - 38, padding - 2);
                    g.DrawString($"{scaleMin}ms", scaleFont, scaleBrush, w - 38, h - padding - 10);

                    var points = new PointF[history.Length];
                    float step = (float)graphW / Math.Max(history.Length - 1, 1);
                    
                    for (int i = 0; i < history.Length; i++)
                    {
                        float x = padding + i * step;
                        float normalized = (float)((history[i] - scaleMin) / (double)scaleRange);
                        float y = padding + (1 - normalized) * graphH;
                        points[i] = new PointF(x, y);
                    }

                    if (points.Length >= 2)
                    {
                        using var path = new System.Drawing.Drawing2D.GraphicsPath();
                        path.AddLines(points);
                        path.AddLine(points[^1].X, points[^1].Y, points[^1].X, h - padding);
                        path.AddLine(points[^1].X, h - padding, points[0].X, h - padding);
                        path.CloseFigure();

                        using var gradientBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                            new Point(0, padding), new Point(0, h - padding),
                            Color.FromArgb(60, 0, 180, 255), Color.FromArgb(5, 0, 180, 255));
                        g.FillPath(gradientBrush, path);

                        using var linePen = new Pen(Color.FromArgb(0, 180, 255), 2);
                        g.DrawLines(linePen, points);

                        int pointInterval = Math.Max(1, history.Length / 12);
                        for (int i = 0; i < points.Length; i += pointInterval)
                        {
                            g.FillEllipse(Brushes.White, points[i].X - 3, points[i].Y - 3, 6, 6);
                            g.FillEllipse(new SolidBrush(Color.FromArgb(0, 180, 255)), points[i].X - 2, points[i].Y - 2, 4, 4);
                        }
                    }
                };

                graphPanel.Controls.AddRange(new Control[] { graphTitle, graphCanvas });

                var latencyPanel = new Panel
                {
                    Size = new Size(530, 100),
                    Location = new Point(15, 418),
                    BackColor = Color.FromArgb(40, 40, 48),
                    Padding = new Padding(15)
                };

                var latencyTitle = new Label
                {
                    Text = "LATÊNCIA",
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    ForeColor = Color.FromArgb(150, 150, 160),
                    Location = new Point(15, 8),
                    AutoSize = true
                };

                long minLat = _pingStatistics.MinimumLatency >= 0 ? _pingStatistics.MinimumLatency : 0;
                long maxLat = _pingStatistics.MaximumLatency >= 0 ? _pingStatistics.MaximumLatency : 1;
                double avgLat = _pingStatistics.AverageLatency >= 0 ? _pingStatistics.AverageLatency : 0;
                long maxScale = Math.Max(maxLat, 1);

                var minBar = CreateLatencyBar("Min", minLat, maxScale, Color.FromArgb(40, 167, 69), new Point(15, 30), 480);
                var avgBar = CreateLatencyBar("Média", (long)avgLat, maxScale, Color.FromArgb(0, 123, 255), new Point(15, 52), 480);
                var maxBar = CreateLatencyBar("Max", maxLat, maxScale, Color.FromArgb(220, 53, 69), new Point(15, 74), 480);

                latencyPanel.Controls.AddRange(new Control[] { latencyTitle, minBar, avgBar, maxBar });

                var summaryPanel = new Panel
                {
                    Size = new Size(530, 45),
                    Location = new Point(15, 525),
                    BackColor = Color.FromArgb(35, 35, 42)
                };

                var summaryLabel = new Label
                {
                    Text = $"Amostras: {history.Length}  |  Sucesso: {_pingStatistics.SuccessfulPings}  |  Falhas: {_pingStatistics.FailedPings}",
                    Font = new Font("Segoe UI", 9f),
                    ForeColor = Color.FromArgb(130, 130, 140),
                    Location = new Point(15, 13),
                    AutoSize = true
                };

                summaryPanel.Controls.Add(summaryLabel);

                var buttonsPanel = new Panel
                {
                    Size = new Size(530, 50),
                    Location = new Point(15, 578),
                    BackColor = Color.Transparent
                };

                var resetButton = new Button 
                { 
                    Text = "Resetar", 
                    Location = new Point(0, 8), 
                    Size = new Size(110, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(220, 53, 69),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                resetButton.FlatAppearance.BorderSize = 0;
                resetButton.Click += (s, e) =>
                {
                    if (MessageBox.Show("Tem certeza que deseja resetar todas as estatísticas de ping?", "Confirmar Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        _pingStatistics.Reset();
                        _alertSystem?.Reset();
                        LogAppEvent("Estatísticas resetadas.");
                        trayIcon.ShowBalloonTip(1500, "Estatísticas", "Estatísticas resetadas!", ToolTipIcon.Info);
                        form.Close();
                    }
                };

                var logsButton = new Button 
                { 
                    Text = "Logs", 
                    Location = new Point(120, 8), 
                    Size = new Size(90, 38),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(108, 117, 125),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                logsButton.FlatAppearance.BorderSize = 0;
                logsButton.Click += (s, e) => ShowPacketLossLog();

                var closeButton = new Button 
                { 
                    Text = "Fechar", 
                    Location = new Point(420, 8), 
                    Size = new Size(110, 38), 
                    DialogResult = DialogResult.OK,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(0, 123, 255),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                closeButton.FlatAppearance.BorderSize = 0;

                buttonsPanel.Controls.AddRange(new Control[] { resetButton, logsButton, closeButton });

                form.Controls.AddRange(new Control[] { headerPanel, cardsPanel, graphPanel, latencyPanel, summaryPanel, buttonsPanel });
                form.AcceptButton = closeButton;
                form.ShowDialog();
                
                LogAppEvent("Estatísticas visualizadas pelo usuário.");
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao exibir estatísticas: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao exibir estatísticas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private Panel CreateStatCard(string title, string value, Color accentColor, Point location, int width, int height = 80)
        {
            var card = new Panel
            {
                Size = new Size(width, height),
                Location = location,
                BackColor = Color.FromArgb(40, 40, 48)
            };

            var accentBar = new Panel
            {
                Size = new Size(width, 3),
                Location = new Point(0, 0),
                BackColor = accentColor
            };

            var titleLabel = new Label
            {
                Text = title.ToUpper(),
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(150, 150, 160),
                Location = new Point(10, 12),
                AutoSize = true
            };

            var valueLabel = new Label
            {
                Text = value,
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(10, 32),
                AutoSize = true
            };

            card.Controls.AddRange(new Control[] { accentBar, titleLabel, valueLabel });
            return card;
        }

        private Panel CreateLatencyBar(string label, long value, long maxValue, Color barColor, Point location, int totalWidth = 420)
        {
            var container = new Panel
            {
                Size = new Size(totalWidth, 22),
                Location = location,
                BackColor = Color.Transparent
            };

            var labelCtrl = new Label
            {
                Text = label,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(180, 180, 190),
                Location = new Point(0, 2),
                Size = new Size(50, 18),
                TextAlign = ContentAlignment.MiddleLeft
            };

            int barBgWidth = totalWidth - 130;
            var barBg = new Panel
            {
                Size = new Size(barBgWidth, 14),
                Location = new Point(55, 4),
                BackColor = Color.FromArgb(60, 60, 70)
            };

            int barWidth = maxValue > 0 ? (int)((value * (barBgWidth - 10.0)) / maxValue) : 0;
            barWidth = Math.Max(barWidth, 2);

            var barFill = new Panel
            {
                Size = new Size(barWidth, 14),
                Location = new Point(0, 0),
                BackColor = barColor
            };

            barBg.Controls.Add(barFill);

            var valueLabel = new Label
            {
                Text = $"{value}ms",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(totalWidth - 65, 2),
                AutoSize = true
            };

            container.Controls.AddRange(new Control[] { labelCtrl, barBg, valueLabel });
            return container;
        }

        private void ResetStatistics()
        {
            try
            {
                var result = MessageBox.Show(
                    "Tem certeza que deseja resetar todas as estatísticas de ping?",
                    "Confirmar Reset",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (result == DialogResult.Yes)
                {
                    _pingStatistics.Reset();
                    _alertSystem?.Reset();
                    _packetLossLog.Clear(); 
                    LogAppEvent("Estatísticas resetadas.");
                    trayIcon.ShowBalloonTip(1500, "Estatísticas", "Estatísticas resetadas!", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao resetar estatísticas: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao resetar estatísticas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RecordPacketLossEvent()
        {
            if (_consecutiveFailedPings > 0)
            {
                _packetLossLog.Add((DateTime.Now, _consecutiveFailedPings));
                while (_packetLossLog.Count > 1000)
                {
                    _packetLossLog.RemoveAt(0);
                }
            }
        }

        private void ShowPacketLossLog()
        {
            using var form = new Form
            {
                Text = "Log de Perdas de Pacotes",
                ClientSize = new Size(400, 350),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(240, 240, 240)
            };

            var headerLabel = new Label
            {
                Text = "Log de Perdas de Pacotes",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50),
                Location = new Point(15, 10),
                AutoSize = true
            };

            var richTextBox = new RichTextBox
            {
                ReadOnly = true,
                Location = new Point(15, 45),
                Size = new Size(370, 220),
                Font = new Font("Consolas", 10f),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            if (_packetLossLog.Count == 0)
            {
                richTextBox.Text = "Nenhuma perda de pacote registrada.";
            }
            else
            {
                var logLines = _packetLossLog
                    .OrderByDescending(x => x.Timestamp)
                    .Select(x => $"{x.Timestamp:dd/MM/yyyy HH:mm:ss} packet loss: {x.ConsecutiveLosses}");
                richTextBox.Text = string.Join(Environment.NewLine, logLines);
            }

            var clearButton = new Button 
            { 
                Text = "Limpar Log", 
                Location = new Point(15, 280), 
                Size = new Size(100, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            clearButton.FlatAppearance.BorderSize = 0;
            clearButton.Click += (s, e) =>
            {
                if (MessageBox.Show("Limpar todo o log de perdas de pacotes?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _packetLossLog.Clear();
                    richTextBox.Text = "Nenhuma perda de pacote registrada.";
                    LogAppEvent("Log de perdas de pacotes limpo.");
                }
            };

            var closeButton = new Button 
            { 
                Text = "Fechar", 
                Location = new Point(285, 280), 
                Size = new Size(100, 30), 
                DialogResult = DialogResult.OK,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 123, 255),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;

            form.Controls.AddRange(new Control[] { headerLabel, richTextBox, clearButton, closeButton });
            form.AcceptButton = closeButton;
            form.ShowDialog();
        }

        private void ExportDataToCsv()
        {
            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    DefaultExt = "csv",
                    FileName = $"ISPing_Stats_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string pingType = _useTcpPing ? $"TCP/{_tcpPingPort}" : "ICMP";
                    StatisticsExporter.ExportToCsv(saveDialog.FileName, _pingStatistics, pingTarget, pingType);
                    LogAppEvent($"Estatísticas exportadas para CSV: {saveDialog.FileName}");

                }
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao exportar para CSV: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao exportar dados: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportDataToJson()
        {
            try
            {
                using var saveDialog = new SaveFileDialog
                {
                    Filter = "JSON files (*.json)|*.json",
                    DefaultExt = "json",
                    FileName = $"ISPing_Stats_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveDialog.ShowDialog() == DialogResult.OK)
                {
                    string pingType = _useTcpPing ? $"TCP/{_tcpPingPort}" : "ICMP";
                    StatisticsExporter.ExportToJson(saveDialog.FileName, _pingStatistics, pingTarget, pingType);
                    LogAppEvent($"Estatísticas exportadas para JSON: {saveDialog.FileName}");

                }
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao exportar para JSON: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao exportar dados: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureAlerts()
        {
            if (_alertSystem == null) return;

            try
            {
                using var alertForm = new Form
                {
                    Text = "Configurar Alertas",
                    ClientSize = new Size(400, 280),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterScreen,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var enableAlertsCheckBox = new CheckBox
                {
                    Text = "Ativar Sistema de Alertas",
                    Checked = _alertSystem.Enabled,
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                var latencyLabel = new Label
                {
                    Text = "Threshold de Latência Alta (ms):",
                    Location = new Point(20, 60),
                    AutoSize = true
                };

                var latencyNumeric = new NumericUpDown
                {
                    Minimum = 10,
                    Maximum = 1000,
                    Value = _alertSystem.LatencyThresholdMs,
                    Location = new Point(250, 57),
                    Width = 100
                };

                var failuresLabel = new Label
                {
                    Text = "Falhas Consecutivas p/ Alertar:",
                    Location = new Point(20, 95),
                    AutoSize = true
                };

                var failuresNumeric = new NumericUpDown
                {
                    Minimum = 1,
                    Maximum = 20,
                    Value = _alertSystem.ConsecutiveFailuresThreshold,
                    Location = new Point(250, 92),
                    Width = 100
                };

                var highLatencyLabel = new Label
                {
                    Text = "Latência Alta Consecutiva p/ Alertar:",
                    Location = new Point(20, 130),
                    AutoSize = true
                };

                var highLatencyNumeric = new NumericUpDown
                {
                    Minimum = 1,
                    Maximum = 20,
                    Value = _alertSystem.ConsecutiveHighLatencyThreshold,
                    Location = new Point(250, 127),
                    Width = 100
                };

                var soundCheckBox = new CheckBox
                {
                    Text = "Tocar som ao alertar",
                    Checked = _alertSystem.PlaySoundOnAlert,
                    Location = new Point(20, 165),
                    AutoSize = true
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(240, 220),
                    Width = 75
                };

                var cancelButton = new Button
                {
                    Text = "Cancelar",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(320, 220),
                    Width = 75
                };

                alertForm.Controls.AddRange(new Control[] {
                    enableAlertsCheckBox, latencyLabel, latencyNumeric,
                    failuresLabel, failuresNumeric, highLatencyLabel, highLatencyNumeric,
                    soundCheckBox, okButton, cancelButton
                });

                alertForm.AcceptButton = okButton;
                alertForm.CancelButton = cancelButton;

                if (alertForm.ShowDialog() == DialogResult.OK)
                {
                    _alertSystem.Enabled = enableAlertsCheckBox.Checked;
                    _alertSystem.LatencyThresholdMs = (int)latencyNumeric.Value;
                    _alertSystem.ConsecutiveFailuresThreshold = (int)failuresNumeric.Value;
                    _alertSystem.ConsecutiveHighLatencyThreshold = (int)highLatencyNumeric.Value;
                    _alertSystem.PlaySoundOnAlert = soundCheckBox.Checked;

                    _settings.AlertsEnabled = _alertSystem.Enabled;
                    _settings.LatencyAlertThresholdMs = _alertSystem.LatencyThresholdMs;
                    _settings.ConsecutiveFailuresAlertThreshold = _alertSystem.ConsecutiveFailuresThreshold;
                    _settings.PlaySoundOnAlert = _alertSystem.PlaySoundOnAlert;
                    _settings.Save();

                    LogAppEvent($"Configurações de alertas atualizadas. Ativado: {_alertSystem.Enabled}");
                    trayIcon.ShowBalloonTip(1500, "Alertas Configurados", "Configurações de alertas salvas!", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao configurar alertas: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao configurar alertas: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TriggerLatencyHoundTracert(long triggerLatencyMs, string target)
        {
            if (_latencyHound == null || _isLatencyHoundTracertRunning) return;

            _isLatencyHoundTracertRunning = true;
            _latencyHound.MarkTracertStarted();

            double baseline = _latencyHound.BaselineLatency;
            double variation = Math.Abs(triggerLatencyMs - baseline);

            LogAppEvent($"Latency Hound: Variação detectada! Atual: {triggerLatencyMs}ms, Baseline: {baseline:F0}ms (Δ{variation:F0}ms). Executando tracert...");

            try
            {
                string tracertOutput = await RunTracerouteWithRawOutput(target);
                string parsedRoute = ParseTracertOutput(tracertOutput);

                _latencyHound.RecordTracertResult(target, triggerLatencyMs, parsedRoute, tracertOutput);

                LogAppEvent($"Latency Hound: Tracert concluído. Rota: {parsedRoute.Replace(Environment.NewLine, " > ")}");
            }
            catch (Exception ex)
            {
                LogAppEvent($"Latency Hound: Erro no tracert: {ex.Message}", "ERROR");
            }
            finally
            {
                _isLatencyHoundTracertRunning = false;
            }
        }

        private async Task<string> RunTracerouteWithRawOutput(string host)
        {
            try
            {
                using var process = new Process 
                { 
                    StartInfo = new ProcessStartInfo("tracert", $"-d -h 15 -w 500 {host}") 
                    { 
                        RedirectStandardOutput = true, 
                        UseShellExecute = false, 
                        CreateNoWindow = true 
                    } 
                };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(15000))
                {
                    try { process.Kill(true); } catch { }
                    return "Erro: tracert expirou (timeout).";
                }
                return output;
            }
            catch (Exception ex)
            {
                return $"Erro: {ex.Message}";
            }
        }

        private async Task RunLatencyHoundTracertManually()
        {
            if (_latencyHound == null || !IsValidHostnameOrIp(pingTarget))
            {
                MessageBox.Show("Alvo inválido para executar tracert.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_isLatencyHoundTracertRunning)
            {
                MessageBox.Show("Um tracert já está em execução. Aguarde.", "Aguarde", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _isLatencyHoundTracertRunning = true;
            trayIcon.ShowBalloonTip(2000, "Latency Hound", $"Executando tracert para {pingTarget}...", ToolTipIcon.Info);
            LogAppEvent($"Latency Hound: Tracert manual iniciado para {pingTarget}");

            try
            {
                string tracertOutput = await RunTracerouteWithRawOutput(pingTarget);
                string parsedRoute = ParseTracertOutput(tracertOutput);

                _latencyHound.RecordTracertResult(pingTarget, _lastLatencyValue, parsedRoute, tracertOutput);

                LogAppEvent($"Latency Hound: Tracert manual concluído. Rota: {parsedRoute.Replace(Environment.NewLine, " > ")}");
            }
            catch (Exception ex)
            {
                LogAppEvent($"Latency Hound: Erro no tracert manual: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao executar tracert: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _isLatencyHoundTracertRunning = false;
            }
        }

        private void ShowLatencyHoundResults()
        {
            if (_latencyHound == null) return;

            var results = _latencyHound.GetAllResults();

            using var form = new Form
            {
                Text = "Latency Hound - Histórico de Tracerts",
                ClientSize = new Size(700, 500),
                FormBorderStyle = FormBorderStyle.SizableToolWindow,
                StartPosition = FormStartPosition.CenterScreen,
                MinimumSize = new Size(500, 300)
            };

            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listView.Columns.Add("Data/Hora", 150);
            listView.Columns.Add("Alvo", 100);
            listView.Columns.Add("Latência", 80);
            listView.Columns.Add("Baseline", 80);
            listView.Columns.Add("Variação", 80);
            listView.Columns.Add("Rota", 200);

            foreach (var result in results)
            {
                double variation = Math.Abs(result.TriggerLatencyMs - result.BaselineLatencyMs);
                var item = new ListViewItem(result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(result.Target);
                item.SubItems.Add($"{result.TriggerLatencyMs}ms");
                item.SubItems.Add($"{result.BaselineLatencyMs:F0}ms");
                item.SubItems.Add($"Δ{variation:F0}ms");
                item.SubItems.Add(result.ParsedRoute.Length > 50 ? result.ParsedRoute.Substring(0, 47) + "..." : result.ParsedRoute);
                item.Tag = result;
                listView.Items.Add(item);
            }

            listView.DoubleClick += (s, e) =>
            {
                if (listView.SelectedItems.Count > 0 && listView.SelectedItems[0].Tag is TracertResult selectedResult)
                {
                    MessageBox.Show(
                        $"Data/Hora: {selectedResult.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                        $"Alvo: {selectedResult.Target}\n" +
                        $"Latência que disparou: {selectedResult.TriggerLatencyMs}ms\n" +
                        $"Baseline: {selectedResult.BaselineLatencyMs:F0}ms\n" +
                        $"Variação: Δ{Math.Abs(selectedResult.TriggerLatencyMs - selectedResult.BaselineLatencyMs):F0}ms\n\n" +
                        $"Rota:\n{selectedResult.ParsedRoute.Replace(" > ", "\n→ ")}",
                        "Detalhes do Tracert",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            };

            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(10) };
            
            var clearButton = new Button { Text = "Limpar Histórico", Location = new Point(10, 10), AutoSize = true };
            clearButton.Click += (s, e) =>
            {
                if (MessageBox.Show("Limpar todo o histórico de tracerts?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _latencyHound?.ClearResults();
                    listView.Items.Clear();
                    LogAppEvent("Latency Hound: Histórico de tracerts limpo.");
                }
            };

            var resetBaselineButton = new Button { Text = "Resetar Baseline", Location = new Point(clearButton.Right + 10, 10), AutoSize = true };
            resetBaselineButton.Click += (s, e) =>
            {
                if (MessageBox.Show("Resetar o baseline de latência? Isso irá recalcular a partir das próximas amostras.", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _latencyHound?.ResetBaseline();
                    LogAppEvent("Latency Hound: Baseline resetado.");
                    trayIcon.ShowBalloonTip(1500, "Latency Hound", "Baseline resetado!", ToolTipIcon.Info);
                }
            };

            var statusLabel = new Label 
            { 
                Text = $"Total: {results.Count} registro(s) | Baseline atual: {(_latencyHound.BaselineLatency >= 0 ? $"{_latencyHound.BaselineLatency:F0}ms" : "N/A")} | Amostras: {_latencyHound.SampleCount}",
                Location = new Point(resetBaselineButton.Right + 20, 15),
                AutoSize = true
            };

            bottomPanel.Controls.AddRange(new Control[] { clearButton, resetBaselineButton, statusLabel });
            form.Controls.Add(listView);
            form.Controls.Add(bottomPanel);
            form.ShowDialog();
        }

        private void ConfigureLatencyHound()
        {
            if (_latencyHound == null) return;

            try
            {
                using var form = new Form
                {
                    Text = "Configurar Latency Hound",
                    ClientSize = new Size(420, 280),
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    StartPosition = FormStartPosition.CenterScreen,
                    MaximizeBox = false,
                    MinimizeBox = false
                };

                var enableCheckBox = new CheckBox
                {
                    Text = "Ativar Latency Hound",
                    Checked = _latencyHound.Enabled,
                    Location = new Point(20, 20),
                    AutoSize = true
                };

                var thresholdLabel = new Label
                {
                    Text = "Threshold de variação (ms):",
                    Location = new Point(20, 60),
                    AutoSize = true
                };

                var thresholdNumeric = new NumericUpDown
                {
                    Minimum = 10,
                    Maximum = 500,
                    Value = _latencyHound.ThresholdMs,
                    Location = new Point(270, 57),
                    Width = 100
                };

                var confirmationLabel = new Label
                {
                    Text = "Leituras consecutivas p/ confirmar:",
                    Location = new Point(20, 95),
                    AutoSize = true
                };

                var confirmationNumeric = new NumericUpDown
                {
                    Minimum = 1,
                    Maximum = 10,
                    Value = _latencyHound.ConfirmationCount,
                    Location = new Point(270, 92),
                    Width = 100
                };

                var minIntervalLabel = new Label
                {
                    Text = "Intervalo mínimo entre tracerts (s):",
                    Location = new Point(20, 130),
                    AutoSize = true
                };

                var minIntervalNumeric = new NumericUpDown
                {
                    Minimum = 5,
                    Maximum = 300,
                    Value = _latencyHound.MinIntervalBetweenTracertsSeconds,
                    Location = new Point(270, 127),
                    Width = 100
                };

                var infoLabel = new Label
                {
                    Text = $"Status: Baseline {(_latencyHound.BaselineLatency >= 0 ? $"{_latencyHound.BaselineLatency:F0}ms" : "N/A")} | Streak: {_latencyHound.CurrentHighVariationStreak}/{_latencyHound.ConfirmationCount}",
                    Location = new Point(20, 165),
                    AutoSize = true,
                    ForeColor = Color.Gray
                };

                var resetBaselineButton = new Button
                {
                    Text = "Resetar Baseline",
                    Location = new Point(20, 190),
                    AutoSize = true
                };
                resetBaselineButton.Click += (s, e) =>
                {
                    _latencyHound.ResetBaseline();
                    infoLabel.Text = "Status: Baseline N/A | Streak: 0";
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(260, 230),
                    Width = 75
                };

                var cancelButton = new Button
                {
                    Text = "Cancelar",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(340, 230),
                    Width = 75
                };

                form.Controls.AddRange(new Control[] 
                { 
                    enableCheckBox, 
                    thresholdLabel, thresholdNumeric,
                    confirmationLabel, confirmationNumeric,
                    minIntervalLabel, minIntervalNumeric,
                    infoLabel, resetBaselineButton,
                    okButton, cancelButton 
                });

                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (form.ShowDialog() == DialogResult.OK)
                {
                    _latencyHound.Enabled = enableCheckBox.Checked;
                    _latencyHound.ThresholdMs = (int)thresholdNumeric.Value;
                    _latencyHound.ConfirmationCount = (int)confirmationNumeric.Value;
                    _latencyHound.MinIntervalBetweenTracertsSeconds = (int)minIntervalNumeric.Value;

                    _settings.LatencyHoundEnabled = _latencyHound.Enabled;
                    _settings.LatencyHoundThresholdMs = _latencyHound.ThresholdMs;
                    _settings.LatencyHoundConfirmationCount = _latencyHound.ConfirmationCount;
                    _settings.LatencyHoundMinIntervalSeconds = _latencyHound.MinIntervalBetweenTracertsSeconds;
                    _settings.Save();

                    LogAppEvent($"Latency Hound: Configurações atualizadas. Ativado: {_latencyHound.Enabled}, Threshold: {_latencyHound.ThresholdMs}ms, Confirmações: {_latencyHound.ConfirmationCount}, Intervalo Min: {_latencyHound.MinIntervalBetweenTracertsSeconds}s");
                    trayIcon.ShowBalloonTip(1500, "Latency Hound", "Configurações salvas!", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao configurar Latency Hound: {ex.Message}", "ERROR");
                MessageBox.Show($"Erro ao configurar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.PingTarget = pingTarget;
                _settings.PingInterval = pingInterval;
                _settings.UseTcpPing = _useTcpPing;
                _settings.TcpPingPort = _tcpPingPort;
                _settings.UseAverageLatencyForIcon = _useAverageLatencyForIcon;
                _settings.AutoCloseDuration = _defaultAutoCloseDuration;
                _settings.ClipboardMonitorEnabled = _clipboardMonitorEnabled;
                _settings.LogRouteChangeEnabled = _logRouteChangeEnabled;
                _settings.NetworkSpeedMonitorEnabled = _networkSpeedMonitorEnabled;
                _settings.SpeedWindowSingleLineMode = _speedWindowSingleLineMode;
                _settings.SelectedNetworkInterfaceId = _selectedNetworkInterface?.Id;

                if (_latencyHound != null)
                {
                    _settings.LatencyHoundEnabled = _latencyHound.Enabled;
                    _settings.LatencyHoundThresholdMs = _latencyHound.ThresholdMs;
                    _settings.LatencyHoundConfirmationCount = _latencyHound.ConfirmationCount;
                    _settings.LatencyHoundMinIntervalSeconds = _latencyHound.MinIntervalBetweenTracertsSeconds;
                }

                _settings.Save();
                LogAppEvent("Configurações salvas com sucesso.");
            }
            catch (Exception ex)
            {
                LogAppEvent($"Erro ao salvar configurações: {ex.Message}", "ERROR");
            }
        }


        private void ExitApplication()
        {
            SaveSettings();
            LogAppEvent("Aplicação encerrada.", "INFO"); NetworkChange.NetworkAvailabilityChanged -= NetworkAvailabilityChangedCallback; NetworkChange.NetworkAddressChanged -= NetworkAddressChangedCallback; LogAppEvent("Monitores de rede desregistrados.");
            _httpClient?.Dispose(); _ipCheckTimer?.Stop(); _ipCheckTimer?.Dispose(); pingTimer?.Stop(); pingTimer?.Dispose(); tracertTimer?.Stop(); tracertTimer?.Dispose(); networkSpeedTimer?.Stop(); networkSpeedTimer?.Dispose(); _uploadCounter?.Dispose(); _downloadCounter?.Dispose(); CloseFloatingSpeedWindow();
            if (_clipboardMonitorForm != null) { if (_clipboardMonitorForm.IsHandleCreated && !_clipboardMonitorForm.IsDisposed) RemoveClipboardFormatListener(_clipboardMonitorForm.Handle); _clipboardMonitorForm.Close(); _clipboardMonitorForm.Dispose(); }
            trayIcon?.Dispose(); _fixedFont?.Dispose(); if (_defaultIcon != null && !_defaultIcon.Equals(SystemIcons.Application)) _defaultIcon.Dispose(); foreach (var fw in _floatingPingWindows.Values.ToList()) { fw.Close(); fw.Dispose(); } _floatingPingWindows.Clear(); Application.Exit();
        }
        protected override void Dispose(bool disposing) { if (disposing) ExitApplication(); base.Dispose(disposing); }

        private class LogDisplayForm : Form
        {
            private RichTextBox _logRichTextBox = new();
            private readonly string _currentLogFilePath;
            private readonly string _logTypeForTitle;
            private readonly ISPing? _mainAppInstance;
            private CheckBox? _showDebugCheckBox;
            private string _rawLogContent = "";

            public LogDisplayForm(string title, string logContent, string logFilePath, ISPing? mainApp)
            {
                _rawLogContent = logContent;
                _currentLogFilePath = logFilePath;
                _logTypeForTitle = title.Contains("Rota") ? "Rotas" : "Eventos";
                _mainAppInstance = mainApp;
                InitializeComponent(title);
                LoadLogData();
            }

            private void InitializeComponent(string title)
            {
                Text = title; ClientSize = new Size(800, 450); StartPosition = FormStartPosition.CenterScreen; MinimizeBox = false; MaximizeBox = true; FormBorderStyle = FormBorderStyle.SizableToolWindow; MinimumSize = new Size(400, 300);
                
                var topPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
                var clearLogButton = new Button { Text = "Limpar Log", Location = new Point(5, 5), AutoSize = true };
                clearLogButton.Click += (s, e) => ClearLog();
                topPanel.Controls.Add(clearLogButton);

                if (_logTypeForTitle == "Eventos")
                {
                    _showDebugCheckBox = new CheckBox
                    {
                        Text = "Mostrar DEBUG",
                        Checked = false,
                        Location = new Point(clearLogButton.Right + 10, clearLogButton.Top),
                        AutoSize = true
                    };
                    _showDebugCheckBox.CheckedChanged += (s, e) => LoadLogData();
                    topPanel.Controls.Add(_showDebugCheckBox);
                }

                _logRichTextBox.Dock = DockStyle.Fill; _logRichTextBox.ReadOnly = true; _logRichTextBox.BackColor = Color.White; _logRichTextBox.Font = new Font("Arial", 9.5f); _logRichTextBox.WordWrap = false; _logRichTextBox.ScrollBars = RichTextBoxScrollBars.Both;
                
                Controls.Add(topPanel); 
                Controls.Add(_logRichTextBox); 
                _logRichTextBox.BringToFront();
            }

            private void LoadLogData()
            {
                if (string.IsNullOrWhiteSpace(_rawLogContent))
                {
                    _logRichTextBox.Text = string.Empty;
                    return;
                }

                bool showDebugEvents = true;
                if (_logTypeForTitle == "Eventos" && _showDebugCheckBox != null)
                {
                    showDebugEvents = _showDebugCheckBox.Checked;
                }

                var lines = _rawLogContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                var filteredLines = new List<string>();

                foreach (string line in lines)
                {
                    if (_logTypeForTitle == "Eventos")
                    {
                        if (showDebugEvents || !line.Contains("[DEBUG]"))
                        {
                            filteredLines.Add(line);
                        }
                    }
                    else
                    {
                        filteredLines.Add(line);
                    }
                }
                
                _logRichTextBox.Text = string.Join(Environment.NewLine, filteredLines);
                ApplyRichTextFormatting();
            }

            private void ApplyRichTextFormatting()
            {
                if (string.IsNullOrWhiteSpace(_logRichTextBox.Text)) return;
                _logRichTextBox.SuspendLayout();
                var dateTimeRegex = new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}\]"); var dateTimeRegexRoute = new Regex(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\]"); var ipInRouteRegex = new Regex(@"(?<=^\s*>\s*)(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}|N/A)\b"); var keywordsRegex = new Regex(@"(Rota alterada de:|para:)"); var logLevelErrorRegex = new Regex(@"\[ERROR\]"); var logLevelWarnRegex = new Regex(@"\[WARN\]"); var logLevelInfoRegex = new Regex(@"\[INFO\]");
                int currentPosition = 0;
                foreach (string line in _logRichTextBox.Lines)
                {
                    if (_logTypeForTitle == "Eventos")
                    {
                        if (dateTimeRegex.Match(line) is Match dtMatch && dtMatch.Success) FormatSelection(_logRichTextBox, dtMatch, Color.DarkSlateGray, FontStyle.Regular, currentPosition);
                        if (logLevelErrorRegex.Match(line) is Match errMatch && errMatch.Success) FormatSelection(_logRichTextBox, errMatch, Color.Red, FontStyle.Bold, currentPosition);
                        if (logLevelWarnRegex.Match(line) is Match warnMatch && warnMatch.Success) FormatSelection(_logRichTextBox, warnMatch, Color.OrangeRed, FontStyle.Bold, currentPosition);
                        if (logLevelInfoRegex.Match(line) is Match infoMatch && infoMatch.Success) FormatSelection(_logRichTextBox, infoMatch, Color.DarkBlue, FontStyle.Regular, currentPosition);
                    }
                    else { if (dateTimeRegexRoute.Match(line) is Match dtMatch && dtMatch.Success) FormatSelection(_logRichTextBox, dtMatch, Color.DarkSlateGray, FontStyle.Bold, currentPosition); foreach (Match keyMatch in keywordsRegex.Matches(line)) FormatSelection(_logRichTextBox, keyMatch, _logRichTextBox.ForeColor, FontStyle.Bold, currentPosition); if (line.TrimStart().StartsWith(">")) { foreach (Match ipMatch in ipInRouteRegex.Matches(line)) FormatSelection(_logRichTextBox, ipMatch, ipMatch.Value == "N/A" ? Color.Red : Color.ForestGreen, FontStyle.Bold, currentPosition); } }
                    currentPosition += line.Length + Environment.NewLine.Length; 
                }
                _logRichTextBox.SelectionStart = 0;
                _logRichTextBox.SelectionLength = 0;
                _logRichTextBox.ResumeLayout(false);
                _logRichTextBox.PerformLayout();
            }
            
            private void FormatSelection(RichTextBox rtb, Match match, Color color, FontStyle style, int offset = 0) { rtb.Select(offset + match.Index, match.Length); rtb.SelectionColor = color; rtb.SelectionFont = new Font(rtb.Font, style); }
            
            private void ClearLog() 
            { 
                if (MessageBox.Show($"Tem certeza que deseja limpar o log de {_logTypeForTitle.ToLower()}?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) 
                { 
                    try 
                    { 
                        File.WriteAllText(_currentLogFilePath, string.Empty); 
                        _rawLogContent = string.Empty; 
                        LoadLogData(); 

                        if (_logTypeForTitle != "Eventos") _mainAppInstance?.LogAppEvent($"Log de {_logTypeForTitle.ToLower()} limpo pelo usuário."); 
                    } 
                    catch (Exception ex) 
                    { 
                        MessageBox.Show($"Erro ao limpar: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                    } 
                } 
            }
        }

        private class ClipboardMonitorForm : Form
        {
            private readonly ISPing _owner;
            public ClipboardMonitorForm(ISPing owner) { _owner = owner; Visible = false; ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; Location = new Point(-2000, -2000); Size = Size.Empty; if (!IsHandleCreated) CreateHandle(); }
            protected override void SetVisibleCore(bool value) { if (!IsHandleCreated) { CreateHandle(); value = false; } base.SetVisibleCore(value); }
            protected override void WndProc(ref Message m) { if (m.Msg == WM_CLIPBOARDUPDATE) _owner.HandleClipboardUpdate(); base.WndProc(ref m); }
        }

        private class AboutForm : Form
        {
            public AboutForm()
            {
                Text = "Sobre ISPing"; ClientSize = new Size(290, 230); FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterScreen; ShowIcon = false;
                var appName = new Label { Text = "ISPing", Font = new Font("Arial", 12F, FontStyle.Bold), AutoSize = true, Location = new Point(12, 12) };
                var version = new Label { Text = $"Versão: {Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "N/A"}", Font = new Font("Arial", 9F), AutoSize = true, Location = new Point(15, 40) };
                var desc = new Label { Text = "Monitora latência ICMP Ping.\nExibe na Barra de Tarefas.\nRecursos: log de rota, monitor de velocidade, clipboard IP.", Font = new Font("Arial", 9F), AutoSize = false, Size = new Size(260, 70), Location = new Point(15, 60) };
                var link = new LinkLabel { Text = "@Caio_Fndo", Font = new Font("Arial", 9F), AutoSize = true, Location = new Point(15, 140), LinkArea = new LinkArea(0, "@Caio_Fndo".Length), Tag = "https://t.me/caio_fndo" };
                link.LinkClicked += (s, e) => { if (s is LinkLabel { Tag: string url }) try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show($"Erro ao abrir: {url}\n{ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error); } };
                var donate = new Button { Text = "ETH: 0xa76...1702", Location = new Point(15, 170), Size = new Size(150, 25) };
                donate.Click += (s, e) => { Clipboard.SetText("0xa76d3566581d5B6BD7F512e4F6DeC7f873581702"); MessageBox.Show("Endereço ETH copiado!", "Copiado", MessageBoxButtons.OK, MessageBoxIcon.Information); };
                var ok = new Button { Text = "OK", Location = new Point(190, 170), Size = new Size(75, 25), DialogResult = DialogResult.OK };
                Controls.AddRange(new Control[] { appName, version, desc, link, donate, ok }); AcceptButton = ok;
            }
        }

        public class FloatingPingWindow : Form
        {
            private Label pingResultLabel = new(); private System.Windows.Forms.Timer pingTimer; private readonly string targetIp; private readonly Queue<string> pingHistory = new(6); private System.Windows.Forms.Timer autoCloseTimer; private TimeSpan _autoCloseDurationInternal; private bool _isClosing = false;
            public FloatingPingWindow(string ipAddress, TimeSpan globalAutoCloseDuration) { targetIp = ipAddress; _autoCloseDurationInternal = (globalAutoCloseDuration == TimeSpan.Zero) ? TimeSpan.Zero : globalAutoCloseDuration; InitializeFloatingWindow(); pingTimer = new System.Windows.Forms.Timer { Interval = 1000 }; pingTimer.Tick += async (s, e) => await UpdatePingDisplay(); pingTimer.Start(); autoCloseTimer = new System.Windows.Forms.Timer(); autoCloseTimer.Tick += (s, e) => { if (!_isClosing && !IsDisposed) { try { if (InvokeRequired) BeginInvoke((Action)Close); else Close(); } catch (ObjectDisposedException) { } } }; SetAutoCloseDuration(_autoCloseDurationInternal); _ = UpdatePingDisplay(); FormClosing += (s, e) => { _isClosing = true; pingTimer.Stop(); autoCloseTimer.Stop(); }; FormClosed += (s, e) => { pingTimer.Dispose(); autoCloseTimer.Dispose(); pingResultLabel.Dispose(); }; }
            private void InitializeFloatingWindow() { FormBorderStyle = FormBorderStyle.FixedToolWindow; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual; ClientSize = new Size(250, 130); Text = $"Ping: {targetIp}"; BackColor = Color.FromArgb(30, 30, 30); pingResultLabel.Dock = DockStyle.Fill; pingResultLabel.TextAlign = ContentAlignment.TopLeft; pingResultLabel.Font = new Font("Arial", 9F); pingResultLabel.Padding = new Padding(5); pingResultLabel.ForeColor = Color.LightGray; Controls.Add(pingResultLabel); Point mousePos = Cursor.Position; Location = new Point(mousePos.X - Width / 2, mousePos.Y - Height / 2); Screen screen = Screen.FromPoint(Location); Left = Math.Max(screen.WorkingArea.Left, Math.Min(Left, screen.WorkingArea.Right - Width)); Top = Math.Max(screen.WorkingArea.Top, Math.Min(Top, screen.WorkingArea.Bottom - Height)); }
            public void SetAutoCloseDuration(TimeSpan newDuration) { _autoCloseDurationInternal = newDuration; autoCloseTimer.Stop(); if (_autoCloseDurationInternal > TimeSpan.Zero) { autoCloseTimer.Interval = Math.Max(100, (int)_autoCloseDurationInternal.TotalMilliseconds); autoCloseTimer.Start(); } }
            private async Task UpdatePingDisplay()
            {
                if (_isClosing || IsDisposed) return; string result; bool success = false;
                try { if (!ISPing.IsValidHostnameOrIp(targetIp)) throw new PingException("Alvo inválido"); long latency = await GetIcmpPingLatencyInternal(targetIp); success = true; result = $"{DateTime.Now:HH:mm:ss} > {targetIp}: {latency}ms"; }
                catch (PingException ex) { result = $"{DateTime.Now:HH:mm:ss} > {targetIp}: {ex.Message.Split(new[] { '.', ':' })[0]}"; }
                catch (Exception ex) { result = $"{DateTime.Now:HH:mm:ss} > {targetIp}: Erro ({ex.GetType().Name})"; }
                pingHistory.Enqueue(result); if (pingHistory.Count > 6) pingHistory.Dequeue(); if (IsDisposed || pingResultLabel.IsDisposed) return;
                string textToDisplay = string.Join(Environment.NewLine, pingHistory.ToArray().Reverse()); Color newBackColor = success ? Color.FromArgb(30, 60, 30) : Color.FromArgb(60, 30, 30);
                Action updateAction = () => { if (!pingResultLabel.IsDisposed) { pingResultLabel.Text = textToDisplay; BackColor = newBackColor; } };
                try { if (pingResultLabel.InvokeRequired) pingResultLabel.BeginInvoke(updateAction); else updateAction(); } catch (ObjectDisposedException) { }
            }
            private async Task<long> GetIcmpPingLatencyInternal(string host)
            {
                using var pingClient = new Ping();
                try { IPAddress[] addresses = await Dns.GetHostAddressesAsync(host); if (addresses.Length == 0) throw new PingException($"Host não resolvido: {host}."); var reply = await pingClient.SendPingAsync(addresses[0], 900); if (reply.Status == IPStatus.Success) return Math.Max(0, reply.RoundtripTime); throw new PingException(reply.Status.ToString()); }
                catch (SocketException sex) { throw new PingException($"Erro DNS: {sex.Message.Split(':')[0]}"); }
            }
        }

        public class OutlineLabel : Label
        {
            private Color _outlineForeColor = Color.Black; [Category("Appearance")] [DefaultValue(typeof(Color), "Black")] public Color OutlineForeColor { get => _outlineForeColor; set { _outlineForeColor = value; Invalidate(); } }
            private float _outlineWidth = 1f; [Category("Appearance")] [DefaultValue(1f)] public float OutlineWidth { get => _outlineWidth; set { _outlineWidth = value; Invalidate(); } }
            public OutlineLabel() { DoubleBuffered = true; SetStyle(ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); UpdateStyles(); }
            protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit; RectangleF textRect = new(Padding.Left, Padding.Top, ClientRectangle.Width - Padding.Horizontal, ClientRectangle.Height - Padding.Vertical); using GraphicsPath path = new(); using StringFormat sf = GetStringFormatFromAlignment(TextAlign); path.AddString(Text, Font.FontFamily, (int)Font.Style, e.Graphics.DpiY * Font.SizeInPoints / 72f, textRect, sf); if (OutlineWidth > 0 && OutlineForeColor != Color.Transparent) { using Pen outlinePen = new(OutlineForeColor, OutlineWidth) { LineJoin = LineJoin.Round }; e.Graphics.DrawPath(outlinePen, path); } using SolidBrush textBrush = new(ForeColor); e.Graphics.FillPath(textBrush, path); }
            private StringFormat GetStringFormatFromAlignment(ContentAlignment alignment)
            {
                StringFormat sf = new();
                switch (alignment) { case ContentAlignment.TopLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Near; break; case ContentAlignment.TopCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Near; break; case ContentAlignment.TopRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Near; break; case ContentAlignment.MiddleLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Center; break; case ContentAlignment.MiddleCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Center; break; case ContentAlignment.MiddleRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Center; break; case ContentAlignment.BottomLeft: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Far; break; case ContentAlignment.BottomCenter: sf.Alignment = StringAlignment.Center; sf.LineAlignment = StringAlignment.Far; break; case ContentAlignment.BottomRight: sf.Alignment = StringAlignment.Far; sf.LineAlignment = StringAlignment.Far; break; default: sf.Alignment = StringAlignment.Near; sf.LineAlignment = StringAlignment.Near; break; }
                if (RightToLeft == RightToLeft.Yes) sf.FormatFlags |= StringFormatFlags.DirectionRightToLeft; return sf;
            }
            private bool ShouldSerializeOutlineForeColor() => OutlineForeColor != Color.Black; private void ResetOutlineForeColor() => OutlineForeColor = Color.Black;
            private bool ShouldSerializeOutlineWidth() => OutlineWidth != 1f; private void ResetOutlineWidth() => OutlineWidth = 1f;
        }

        public class FloatingSpeedWindow : Form
        {
            private OutlineLabel speedLabel;
            private bool _isSingleLineMode = false;

            public FloatingSpeedWindow()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                
                BackColor = Color.Black;
                Opacity = 0.70;

                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                this.Padding = new Padding(1);

                speedLabel = new OutlineLabel
                {
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Arial", 10f, FontStyle.Bold),
                    ForeColor = Color.White,
                    OutlineForeColor = Color.Black,
                    OutlineWidth = 2f,
                    BackColor = Color.Transparent,
                    AutoSize = true,
                    Padding = new Padding(2, 1, 2, 1),
                    Text = $"↑ ...{Environment.NewLine}↓ ...{Environment.NewLine}Jitter: N/A"
                };
                Controls.Add(speedLabel);
            }

            public void SetDisplayMode(bool singleLine)
            {
                _isSingleLineMode = singleLine;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (Pen outlinePen = new Pen(Color.White, 1f))
                {
                    e.Graphics.DrawRectangle(outlinePen, 0, 0, this.ClientSize.Width - 1, this.ClientSize.Height - 1);
                }
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= 0x00000020 | 0x00080000;
                    return cp;
                }
            }
            protected override void OnLoad(EventArgs e) { base.OnLoad(e); if (Visible) RepositionWindow(); }
            
            public void SetSpeedText(string uploadInfo, string downloadInfo, string jitterInfo)
            {
                if (IsDisposed || speedLabel.IsDisposed) return;
                
                string combinedText;
                if (_isSingleLineMode)
                {
                    combinedText = $"{uploadInfo} | {downloadInfo} | {jitterInfo}";
                }
                else
                {
                    combinedText = $"{uploadInfo}{Environment.NewLine}{downloadInfo}{Environment.NewLine}{jitterInfo}";
                }
                
                Action updateAction = () => {
                    if (IsDisposed || speedLabel.IsDisposed) return;
                    if (speedLabel.Text != combinedText)
                    {
                        speedLabel.Text = combinedText;
                        if (IsHandleCreated && Visible) RepositionWindow();
                    }
                };

                if (InvokeRequired)
                {
                    try { BeginInvoke(updateAction); }
                    catch (ObjectDisposedException) { }
                    catch (InvalidOperationException) { }
                }
                else
                {
                    updateAction();
                }
            }
            private void RepositionWindow() {
                if (IsDisposed || !IsHandleCreated || !Visible) return;
                PerformLayout();
                Screen? screen = Screen.FromPoint(Location) ?? Screen.PrimaryScreen;
                if (screen == null) return;
                Rectangle workingArea = screen.WorkingArea;
                int margin = 5;
                int x = Math.Max(workingArea.Left + margin, workingArea.Right - Width - margin);
                int y = Math.Max(workingArea.Top + margin, workingArea.Bottom - Height - margin);
                if (Location.X != x || Location.Y != y) Location = new Point(x, y);
            }
        }
    }
    public class PingException : Exception { public PingException(string message) : base(message) { } public PingException(string message, Exception innerException) : base(message, innerException) { } }
    static class Program { [STAThread] static void Main(string[] args) { ApplicationConfiguration.Initialize(); Application.Run(new ISPing(args.Length > 0 ? args[0] : null)); } }
}
