using System;
using System.Media;

namespace ISPing
{
    public class AlertSystem
    {
        private readonly Action<string, string, int> _showNotification;
        private int _consecutiveHighLatency;
        private int _consecutiveFailures;
        private string? _lastPublicIp;
        private DateTime _lastAlertTime = DateTime.MinValue;
        private static readonly TimeSpan AlertCooldown = TimeSpan.FromSeconds(30);

        public bool Enabled { get; set; }
        public int LatencyThresholdMs { get; set; } = 100;
        public int ConsecutiveFailuresThreshold { get; set; } = 3;
        public int ConsecutiveHighLatencyThreshold { get; set; } = 5;
        public bool PlaySoundOnAlert { get; set; }

        public AlertSystem(Action<string, string, int> showNotification)
        {
            _showNotification = showNotification ?? throw new ArgumentNullException(nameof(showNotification));
        }

        public void CheckLatency(long latencyMs, bool success)
        {
            if (!Enabled)
                return;

            if (!success)
            {
                _consecutiveHighLatency = 0;
                _consecutiveFailures++;

                if (_consecutiveFailures >= ConsecutiveFailuresThreshold)
                {
                    TriggerAlert(
                        "Falha de Conexão",
                        $"Detectadas {_consecutiveFailures} falhas consecutivas de ping!",
                        AlertLevel.Warning
                    );
                }
            }
            else
            {
                if (_consecutiveFailures > 0)
                {
                    _consecutiveFailures = 0;
                }

                if (latencyMs >= LatencyThresholdMs)
                {
                    _consecutiveHighLatency++;

                    if (_consecutiveHighLatency >= ConsecutiveHighLatencyThreshold)
                    {
                        TriggerAlert(
                            "Latência Alta",
                            $"Latência de {latencyMs}ms detectada ({ConsecutiveHighLatencyThreshold}x consecutivas acima de {LatencyThresholdMs}ms)!",
                            AlertLevel.Warning
                        );
                        _consecutiveHighLatency = 0;
                    }
                }
                else
                {
                    _consecutiveHighLatency = 0;
                }
            }
        }

        public void CheckPublicIpChange(string newPublicIp)
        {
            if (!Enabled || string.IsNullOrEmpty(newPublicIp) || newPublicIp == "N/A")
                return;

            if (_lastPublicIp != null && _lastPublicIp != newPublicIp && _lastPublicIp != "N/A")
            {
                TriggerAlert(
                    "IP Público Alterado",
                    $"Seu IP público mudou de {_lastPublicIp} para {newPublicIp}",
                    AlertLevel.Info
                );
            }

            _lastPublicIp = newPublicIp;
        }

        private void TriggerAlert(string title, string message, AlertLevel level)
        {
            if (DateTime.Now - _lastAlertTime < AlertCooldown)
                return;

            _lastAlertTime = DateTime.Now;

            int duration = level == AlertLevel.Critical ? 5000 : 3000;
            _showNotification(title, message, duration);

            if (PlaySoundOnAlert)
            {
                try
                {
                    SystemSound sound = level switch
                    {
                        AlertLevel.Critical => SystemSounds.Hand,
                        AlertLevel.Warning => SystemSounds.Exclamation,
                        AlertLevel.Info => SystemSounds.Asterisk,
                        _ => SystemSounds.Beep
                    };
                    sound.Play();
                }
                catch
                {
                }
            }
        }

        public void Reset()
        {
            _consecutiveHighLatency = 0;
            _consecutiveFailures = 0;
        }

        private enum AlertLevel
        {
            Info,
            Warning,
            Critical
        }
    }
}
