using System;
using System.Collections.Generic;
using System.Linq;

namespace ISPing
{
    public class PingStatistics
    {
        private readonly Queue<long> _latencyHistory;
        private readonly int _maxHistorySize;
        private int _totalPings;
        private int _failedPings;
        private long _lastLatency = -1;
        private long _previousLatency = -1;
        private readonly List<long> _jitterSamples = new();
        private readonly DateTime _sessionStart;

        public PingStatistics(int maxHistorySize = 100)
        {
            _maxHistorySize = maxHistorySize;
            _latencyHistory = new Queue<long>(maxHistorySize);
            _sessionStart = DateTime.Now;
        }

        public void AddMeasurement(long latencyMs)
        {
            _totalPings++;
            
            if (latencyMs < 0)
            {
                _failedPings++;
                _previousLatency = _lastLatency;
                _lastLatency = -1;
                return;
            }

            // Calculate jitter (difference from previous successful ping)
            if (_previousLatency >= 0 && _lastLatency >= 0)
            {
                long jitter = Math.Abs(latencyMs - _lastLatency);
                _jitterSamples.Add(jitter);
                if (_jitterSamples.Count > _maxHistorySize)
                    _jitterSamples.RemoveAt(0);
            }

            _previousLatency = _lastLatency;
            _lastLatency = latencyMs;

            _latencyHistory.Enqueue(latencyMs);
            
            if (_latencyHistory.Count > _maxHistorySize)
            {
                _latencyHistory.Dequeue();
            }
        }

        public void Reset()
        {
            _latencyHistory.Clear();
            _jitterSamples.Clear();
            _totalPings = 0;
            _failedPings = 0;
            _lastLatency = -1;
            _previousLatency = -1;
        }

        public long MinimumLatency => _latencyHistory.Any() ? _latencyHistory.Min() : -1;

        public long MaximumLatency => _latencyHistory.Any() ? _latencyHistory.Max() : -1;

        public double AverageLatency => _latencyHistory.Any() ? _latencyHistory.Average() : -1;

        public double StandardDeviation
        {
            get
            {
                if (_latencyHistory.Count < 2)
                    return 0;

                double avg = AverageLatency;
                double sumOfSquares = _latencyHistory.Sum(val => Math.Pow(val - avg, 2));
                return Math.Sqrt(sumOfSquares / _latencyHistory.Count);
            }
        }

        public double Jitter => _jitterSamples.Count > 0 ? _jitterSamples.Average() : 0;

        public long LastLatency => _lastLatency;

        public TimeSpan SessionUptime => DateTime.Now - _sessionStart;

        public int QualityScore
        {
            get
            {
                if (_totalPings == 0) return 0;

                // 100 pontos
                double score = 100;

                score -= Math.Min(PacketLossPercentage * 4, 40);

                double avgLat = AverageLatency >= 0 ? AverageLatency : 0;
                if (avgLat > 40) score -= Math.Min((avgLat - 40) * 0.3, 30);

                if (Jitter > 5) score -= Math.Min((Jitter - 5) * 0.5, 20);

                if (StandardDeviation > 10) score -= Math.Min((StandardDeviation - 10) * 0.2, 10);

                return Math.Max(0, Math.Min(100, (int)Math.Round(score)));
            }
        }

        // rating de qualidade com base score
        public string QualityRating
        {
            get
            {
                int score = QualityScore;
                if (score >= 90) return "Excelente";
                if (score >= 75) return "Bom";
                if (score >= 50) return "Regular";
                if (score >= 25) return "Ruim";
                return "Crítico";
            }
        }

        public double PacketLossPercentage => _totalPings > 0 ? (_failedPings * 100.0 / _totalPings) : 0;

        public int TotalPings => _totalPings;

        public int FailedPings => _failedPings;

        public int SuccessfulPings => _totalPings - _failedPings;

        public IReadOnlyCollection<long> GetHistory() => _latencyHistory.ToList().AsReadOnly();

        public string GetSummary()
        {
            if (_totalPings == 0)
                return "Nenhuma estatística disponível ainda.";

            return $"Estatísticas de Ping:\n" +
                   $"  Total: {_totalPings} pings ({SuccessfulPings} sucesso, {_failedPings} falhas)\n" +
                   $"  Perda de Pacotes: {PacketLossPercentage:F2}%\n" +
                   $"  Latência Min: {MinimumLatency}ms\n" +
                   $"  Latência Max: {MaximumLatency}ms\n" +
                   $"  Latência Média: {AverageLatency:F2}ms\n" +
                   $"  Desvio Padrão: {StandardDeviation:F2}ms\n" +
                   $"  Jitter: {Jitter:F2}ms\n" +
                   $"  Qualidade: {QualityScore}/100 ({QualityRating})";
        }
    }
}
