using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace IPGeoLocator.Services
{
    public interface IPerformanceService
    {
        void StartOperation(string operationName);
        void EndOperation(string operationName);
        void RecordMetric(string metricName, double value);
        PerformanceMetrics GetCurrentMetrics();
        void ResetMetrics();
    }

    public class PerformanceMetrics
    {
        public double AverageLookupTimeMs { get; set; }
        public double AverageGeolocationTimeMs { get; set; }
        public double AverageThreatTimeMs { get; set; }
        public double AverageTimeTimeMs { get; set; }
        public int TotalLookups { get; set; }
        public int TotalOperations { get; set; }
        public Dictionary<string, double> OperationTimes { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, double> CustomMetrics { get; set; } = new Dictionary<string, double>();
        public Dictionary<string, int> OperationCounts { get; set; } = new Dictionary<string, int>();
        public DateTime LastReset { get; set; } = DateTime.UtcNow;
    }

    public class PerformanceService : IPerformanceService
    {
        private readonly ConcurrentDictionary<string, Stopwatch> _activeOperations = new ConcurrentDictionary<string, Stopwatch>();
        private readonly ConcurrentDictionary<string, List<double>> _operationTimes = new ConcurrentDictionary<string, List<double>>();
        private readonly ConcurrentDictionary<string, List<double>> _customMetrics = new ConcurrentDictionary<string, List<double>>();
        private readonly object _lock = new object();

        public void StartOperation(string operationName)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            _activeOperations[operationName] = stopwatch;
        }

        public void EndOperation(string operationName)
        {
            if (_activeOperations.TryRemove(operationName, out var stopwatch))
            {
                stopwatch.Stop();
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                
                if (!_operationTimes.ContainsKey(operationName))
                    _operationTimes[operationName] = new List<double>();
                
                _operationTimes[operationName].Add(elapsedMs);
            }
        }

        public void RecordMetric(string metricName, double value)
        {
            if (!_customMetrics.ContainsKey(metricName))
                _customMetrics[metricName] = new List<double>();
            
            _customMetrics[metricName].Add(value);
        }

        public PerformanceMetrics GetCurrentMetrics()
        {
            var metrics = new PerformanceMetrics();
            
            // Calculate averages for known operations
            if (_operationTimes.ContainsKey("IP_Lookup") && _operationTimes["IP_Lookup"].Any())
            {
                metrics.AverageLookupTimeMs = _operationTimes["IP_Lookup"].Average();
                metrics.TotalLookups = _operationTimes["IP_Lookup"].Count;
            }
            
            if (_operationTimes.ContainsKey("Geolocation") && _operationTimes["Geolocation"].Any())
            {
                metrics.AverageGeolocationTimeMs = _operationTimes["Geolocation"].Average();
            }
            
            if (_operationTimes.ContainsKey("ThreatCheck") && _operationTimes["ThreatCheck"].Any())
            {
                metrics.AverageThreatTimeMs = _operationTimes["ThreatCheck"].Average();
            }
            
            if (_operationTimes.ContainsKey("LocalTime") && _operationTimes["LocalTime"].Any())
            {
                metrics.AverageTimeTimeMs = _operationTimes["LocalTime"].Average();
            }
            
            // Populate operation times and counts
            foreach (var kvp in _operationTimes)
            {
                if (kvp.Value.Any())
                {
                    metrics.OperationTimes[kvp.Key] = kvp.Value.Average();
                    metrics.OperationCounts[kvp.Key] = kvp.Value.Count;
                }
            }
            
            metrics.TotalOperations = _operationTimes.Sum(kvp => kvp.Value.Count);
            
            // Populate custom metrics
            foreach (var kvp in _customMetrics)
            {
                if (kvp.Value.Any())
                {
                    metrics.CustomMetrics[kvp.Key] = kvp.Value.Average();
                }
            }
            
            metrics.LastReset = DateTime.UtcNow;
            
            return metrics;
        }

        public void ResetMetrics()
        {
            _activeOperations.Clear();
            _operationTimes.Clear();
            _customMetrics.Clear();
        }

        public double GetAverageOperationTime(string operationName)
        {
            if (_operationTimes.ContainsKey(operationName) && _operationTimes[operationName].Any())
                return _operationTimes[operationName].Average();
            return 0;
        }

        public int GetOperationCount(string operationName)
        {
            if (_operationTimes.ContainsKey(operationName))
                return _operationTimes[operationName].Count;
            return 0;
        }
    }
}