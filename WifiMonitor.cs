using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace ISPing
{
    public class WifiInfo
    {
        public string Ssid { get; set; } = "N/A";
        public string SignalStrength { get; set; } = "N/A"; 
        public int SignalPercentage { get; set; } = 0;
        public int Rssi { get; set; } = 0; 
        public string Channel { get; set; } = "N/A";
        public string RadioType { get; set; } = "N/A";
        public string FrequencyBand { get; set; } = "N/A"; // 2.4 ou 5GHz
        public string LinkSpeed { get; set; } = "N/A"; 
        public string State { get; set; } = "Desconhecido";
        public bool IsConnected { get; set; } = false;
        public string ErrorMessage { get; set; } = "";
    }

    public class WifiMonitor
    {
        public WifiInfo GetWifiInfo()
        {
            var info = new WifiInfo();
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show interfaces",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8 
                    }
                };

                process.Start();
                
                string output = "";
                var outputTask = Task.Run(() => process.StandardOutput.ReadToEnd());
                
                if (process.WaitForExit(1000)) 
                {
                    output = outputTask.Result;
                }
                else
                {
                    try { process.Kill(); } catch { }
                    info.ErrorMessage = "Timeout ao executar netsh";
                    return info;
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    info.ErrorMessage = "Sem saída do netsh";
                    return info;
                }

                var stateMatch = Regex.Match(output, @"^\s*(?:State|Estado)\s*:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (stateMatch.Success)
                {
                    info.State = stateMatch.Groups[1].Value.Trim();
                    info.IsConnected = info.State.ToLower().Contains("connected") || info.State.ToLower().Contains("conectado");
                }

                if (!info.IsConnected)
                {
                    return info;
                }
                
                var ssidMatch = Regex.Match(output, @"^\s*SSID\s*:\s*(.*)$", RegexOptions.Multiline);
                if (ssidMatch.Success) info.Ssid = ssidMatch.Groups[1].Value.Trim();

                var signalMatch = Regex.Match(output, @"^\s*(?:Signal|Sinal)\s*:\s*(\d+)%", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (signalMatch.Success && int.TryParse(signalMatch.Groups[1].Value, out int signal))
                {
                    info.SignalPercentage = signal;
                    info.SignalStrength = $"{signal}%";
                    info.Rssi = (signal / 2) - 100;
                }

                var channelMatch = Regex.Match(output, @"^\s*(?:Channel|Canal)\s*:\s*(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (channelMatch.Success)
                {
                    info.Channel = channelMatch.Groups[1].Value.Trim();
                    if (int.TryParse(info.Channel, out int chNum))
                    {
                         info.FrequencyBand = chNum > 14 ? "5GHz" : "2.4GHz";
                    }
                }

                var radioMatch = Regex.Match(output, @"^\s*(?:Radio type|Tipo de rádio)\s*:\s*(.*)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (radioMatch.Success)
                {
                    info.RadioType = radioMatch.Groups[1].Value.Trim();
                }

                
                var rxMatch = Regex.Match(output, @"^\s*(?:Receive rate|Taxa de recepção).*?:\s*([\d\.]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var txMatch = Regex.Match(output, @"^\s*(?:Transmit rate|Taxa de transmissão).*?:\s*([\d\.]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);

                if (rxMatch.Success && txMatch.Success)
                {
                    info.LinkSpeed = $"{rxMatch.Groups[1].Value}/{txMatch.Groups[1].Value} Mbps";
                }
                else 
                {
                    var combinedMatch = Regex.Match(output, @"^\s*(?:Link speed|Velocidade).*?:\s*([\d\.]+\s*/\s*[\d\.]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    if (combinedMatch.Success)
                    {
                         info.LinkSpeed = $"{combinedMatch.Groups[1].Value} Mbps";
                    }
                    else
                    {
                        var singleMatch = Regex.Match(output, @"^\s*(?:Link speed|Velocidade).*?:\s*([\d\.]+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                        if (singleMatch.Success)
                        {
                            info.LinkSpeed = $"{singleMatch.Groups[1].Value} Mbps";
                        }
                        else if (txMatch.Success)
                        {
                             info.LinkSpeed = $"{txMatch.Groups[1].Value} Mbps (Tx)";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = $"Erro: {ex.Message}";
            }

            return info;
        }
    }
}
