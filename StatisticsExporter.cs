using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ISPing
{
    public class StatisticsExporter
    {
        public static void ExportToCsv(string filePath, PingStatistics stats, string target, string pingType)
        {
            var csv = new StringBuilder();
            
            csv.AppendLine("ISPing Statistics Export");
            csv.AppendLine($"Export Date,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Ping Target,{target}");
            csv.AppendLine($"Ping Type,{pingType}");
            csv.AppendLine();
            
            csv.AppendLine("Metric,Value");
            csv.AppendLine($"Total Pings,{stats.TotalPings}");
            csv.AppendLine($"Successful Pings,{stats.SuccessfulPings}");
            csv.AppendLine($"Failed Pings,{stats.FailedPings}");
            csv.AppendLine($"Packet Loss %,{stats.PacketLossPercentage:F2}");
            csv.AppendLine($"Min Latency (ms),{stats.MinimumLatency}");
            csv.AppendLine($"Max Latency (ms),{stats.MaximumLatency}");
            csv.AppendLine($"Avg Latency (ms),{stats.AverageLatency:F2}");
            csv.AppendLine($"Std Deviation (ms),{stats.StandardDeviation:F2}");
            csv.AppendLine();
            
            csv.AppendLine("Sample Number,Latency (ms)");
            int sampleNumber = 1;
            foreach (var latency in stats.GetHistory())
            {
                csv.AppendLine($"{sampleNumber},{latency}");
                sampleNumber++;
            }
            
            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        public static void ExportToJson(string filePath, PingStatistics stats, string target, string pingType)
        {
            var data = new
            {
                ExportInfo = new
                {
                    ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    PingTarget = target,
                    PingType = pingType
                },
                Summary = new
                {
                    TotalPings = stats.TotalPings,
                    SuccessfulPings = stats.SuccessfulPings,
                    FailedPings = stats.FailedPings,
                    PacketLossPercentage = Math.Round(stats.PacketLossPercentage, 2),
                    MinLatencyMs = stats.MinimumLatency,
                    MaxLatencyMs = stats.MaximumLatency,
                    AvgLatencyMs = Math.Round(stats.AverageLatency, 2),
                    StdDeviationMs = Math.Round(stats.StandardDeviation, 2)
                },
                LatencyHistory = stats.GetHistory().ToArray()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        public static void ExportEventLog(string sourceLogPath, string destinationPath)
        {
            if (!File.Exists(sourceLogPath))
                throw new FileNotFoundException("Log file not found", sourceLogPath);

            File.Copy(sourceLogPath, destinationPath, overwrite: true);
        }
    }
}
