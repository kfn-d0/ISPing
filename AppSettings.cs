using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ISPing
{
    public class AppSettings
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "ISPing"
        );
        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        public string PingTarget { get; set; } = "8.8.8.8";
        public int PingInterval { get; set; } = 1000;
        public bool UseTcpPing { get; set; } = false;
        public int TcpPingPort { get; set; } = 443;

        public bool UseAverageLatencyForIcon { get; set; } = true;
        public TimeSpan AutoCloseDuration { get; set; } = TimeSpan.FromSeconds(10);

        public bool ClipboardMonitorEnabled { get; set; } = false;
        public bool LogRouteChangeEnabled { get; set; } = false;
        public bool NetworkSpeedMonitorEnabled { get; set; } = false;
        public bool SpeedWindowSingleLineMode { get; set; } = false;
        public string? SelectedNetworkInterfaceId { get; set; }

        public bool AlertsEnabled { get; set; } = false;
        public int LatencyAlertThresholdMs { get; set; } = 100;
        public int ConsecutiveFailuresAlertThreshold { get; set; } = 3;
        public bool PlaySoundOnAlert { get; set; } = false;

        public bool LatencyHoundEnabled { get; set; } = true;
        public int LatencyHoundThresholdMs { get; set; } = 50;
        public int LatencyHoundConfirmationCount { get; set; } = 3;
        public int LatencyHoundMinIntervalSeconds { get; set; } = 300;

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                return settings ?? new AppSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao carregar configurações: {ex.Message}");
                return new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Converters = { new TimeSpanConverter() }
                };
                
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao salvar configurações: {ex.Message}");
            }
        }

        private class TimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return TimeSpan.Parse(reader.GetString() ?? "00:00:00");
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }
    }
}
