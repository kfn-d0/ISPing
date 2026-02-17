using System;
using System.Collections.Generic;
using System.Linq;

namespace ISPing
{
    public class TracertResult
    {
        public DateTime Timestamp { get; set; }
        public string Target { get; set; } = string.Empty;
        public long TriggerLatencyMs { get; set; }
        public double BaselineLatencyMs { get; set; }
        public string ParsedRoute { get; set; } = string.Empty;
        public string RawOutput { get; set; } = string.Empty;
    }

    public class LatencyHound
    {
        private readonly Queue<long> _latencyHistory;
        private readonly List<TracertResult> _tracertResults;
        private DateTime _lastTracertTime = DateTime.MinValue;
        private double _lastBaselineWhenTriggered = -1;
        private const int BASELINE_SAMPLE_COUNT = 10;
        private readonly object _lock = new();
        private int _consecutiveHighVariationCount = 0;

        public bool Enabled { get; set; } = true;
        public int ThresholdMs { get; set; } = 10;
        public int ConfirmationCount { get; set; } = 3;
        public int MinIntervalBetweenTracertsSeconds { get; set; } = 300;

        public double BaselineLatency
        {
            get
            {
                lock (_lock)
                {
                    return _latencyHistory.Count > 0 ? _latencyHistory.Average() : -1;
                }
            }
        }

        public int SampleCount
        {
            get
            {
                lock (_lock)
                {
                    return _latencyHistory.Count;
                }
            }
        }

        public int ResultCount
        {
            get
            {
                lock (_lock)
                {
                    return _tracertResults.Count;
                }
            }
        }

        public int CurrentHighVariationStreak
        {
            get
            {
                lock (_lock)
                {
                    return _consecutiveHighVariationCount;
                }
            }
        }

        public LatencyHound()
        {
            _latencyHistory = new Queue<long>(BASELINE_SAMPLE_COUNT + 1);
            _tracertResults = new List<TracertResult>();
        }

        public void AddLatencySample(long latencyMs)
        {
            if (latencyMs < 0) return;

            lock (_lock)
            {
                if (_latencyHistory.Count < 3)
                {
                    _latencyHistory.Enqueue(latencyMs);
                    return;
                }

                double currentBaseline = _latencyHistory.Average();
                double variation = Math.Abs(latencyMs - currentBaseline);

                if (variation < ThresholdMs)
                {
                    _latencyHistory.Enqueue(latencyMs);
                    if (_latencyHistory.Count > BASELINE_SAMPLE_COUNT)
                    {
                        _latencyHistory.Dequeue();
                    }
                }
            }
        }

        public bool ShouldTriggerTracert(long currentLatencyMs)
        {
            if (!Enabled || currentLatencyMs < 0)
                return false;

            lock (_lock)
            {
                if (_latencyHistory.Count < 3)
                    return false;

                double baseline = _latencyHistory.Average();
                double variation = Math.Abs(currentLatencyMs - baseline);

                if (variation >= ThresholdMs)
                {
                    _consecutiveHighVariationCount++;

                    if (_consecutiveHighVariationCount >= ConfirmationCount)
                    {
                        if ((DateTime.Now - _lastTracertTime).TotalSeconds < MinIntervalBetweenTracertsSeconds)
                            return false;

                        if (_lastBaselineWhenTriggered > 0 && Math.Abs(baseline - _lastBaselineWhenTriggered) < ThresholdMs / 2)
                        {
                            return false;
                        }

                        return true;
                    }
                }
                else
                {
                    _consecutiveHighVariationCount = 0;
                }

                return false;
            }
        }

        public void MarkTracertStarted()
        {
            lock (_lock)
            {
                _lastTracertTime = DateTime.Now;
                _lastBaselineWhenTriggered = _latencyHistory.Count > 0 ? _latencyHistory.Average() : -1;
                _consecutiveHighVariationCount = 0;
            }
        }

        public void RecordTracertResult(string target, long triggerLatencyMs, string parsedRoute, string rawOutput)
        {
            lock (_lock)
            {
                var result = new TracertResult
                {
                    Timestamp = DateTime.Now,
                    Target = target,
                    TriggerLatencyMs = triggerLatencyMs,
                    BaselineLatencyMs = _latencyHistory.Count > 0 ? _latencyHistory.Average() : -1,
                    ParsedRoute = parsedRoute,
                    RawOutput = rawOutput
                };

                _tracertResults.Add(result);

                while (_tracertResults.Count > 50)
                {
                    _tracertResults.RemoveAt(0);
                }
            }
        }

        public TracertResult? GetLatestResult(string target)
        {
            lock (_lock)
            {
                return _tracertResults
                    .Where(r => r.Target.Equals(target, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.Timestamp)
                    .FirstOrDefault();
            }
        }

        public IReadOnlyList<TracertResult> GetResults(string target)
        {
            lock (_lock)
            {
                return _tracertResults
                    .Where(r => r.Target.Equals(target, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(r => r.Timestamp)
                    .ToList()
                    .AsReadOnly();
            }
        }

        public IReadOnlyList<TracertResult> GetAllResults()
        {
            lock (_lock)
            {
                return _tracertResults
                    .OrderByDescending(r => r.Timestamp)
                    .ToList()
                    .AsReadOnly();
            }
        }

        public void ClearResults()
        {
            lock (_lock)
            {
                _tracertResults.Clear();
            }
        }

        public void ResetBaseline()
        {
            lock (_lock)
            {
                _latencyHistory.Clear();
                _consecutiveHighVariationCount = 0;
                _lastBaselineWhenTriggered = -1;
            }
        }

        public bool IsMinIntervalActive
        {
            get
            {
                lock (_lock)
                {
                    return (DateTime.Now - _lastTracertTime).TotalSeconds < MinIntervalBetweenTracertsSeconds;
                }
            }
        }

        public int MinIntervalRemainingSeconds
        {
            get
            {
                lock (_lock)
                {
                    var elapsed = (DateTime.Now - _lastTracertTime).TotalSeconds;
                    var remaining = MinIntervalBetweenTracertsSeconds - elapsed;
                    return remaining > 0 ? (int)remaining : 0;
                }
            }
        }

        [Obsolete("Use MinIntervalBetweenTracertsSeconds instead")]
        public int CooldownSeconds 
        { 
            get => MinIntervalBetweenTracertsSeconds; 
            set => MinIntervalBetweenTracertsSeconds = value; 
        }
        
        [Obsolete("Use IsMinIntervalActive instead")]
        public bool IsCooldownActive => IsMinIntervalActive;
        
        [Obsolete("Use MinIntervalRemainingSeconds instead")]
        public int CooldownRemainingSeconds => MinIntervalRemainingSeconds;
    }
}
