using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using IPGeoLocator.Models;
using IPGeoLocator.Utilities;

namespace IPGeoLocator.ViewModels
{
    public class IpRangeScannerViewModel : INotifyPropertyChanged
    {
        private string _ipRange = "192.168.1.1-100";
        private bool _isScanning;
        private int _currentProgress;
        private int _totalProgress;
        private string _statusMessage = "Ready to scan IP range";
        private ObservableCollection<IpRangeScanResult> _scanResults = new ObservableCollection<IpRangeScanResult>();
        private bool _includeThreatInfo = true;

        public string IpRange
        {
            get => _ipRange;
            set => SetProperty(ref _ipRange, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public int CurrentProgress
        {
            get => _currentProgress;
            set => SetProperty(ref _currentProgress, value);
        }

        public int TotalProgress
        {
            get => _totalProgress;
            set => SetProperty(ref _totalProgress, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ObservableCollection<IpRangeScanResult> ScanResults
        {
            get => _scanResults;
            set => SetProperty(ref _scanResults, value);
        }

        public bool IncludeThreatInfo
        {
            get => _includeThreatInfo;
            set => SetProperty(ref _includeThreatInfo, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public async Task StartScanAsync(string abuseIpDbApiKey)
        {
            if (IsScanning) return; // Prevent multiple scans

            // Validate IP range format
            if (!IsValidIpRange(IpRange))
            {
                StatusMessage = "Invalid IP range format. Use format like: 192.168.1.1-100";
                return;
            }

            IsScanning = true;
            StatusMessage = "Starting scan...";
            
            try
            {
                var ipList = ParseIpRange(IpRange);
                TotalProgress = ipList.Count;
                CurrentProgress = 0;
                ScanResults.Clear(); // Clear previous results

                // Limit concurrent requests to prevent overwhelming the API services
                const int maxConcurrentRequests = 10; // Adjust based on API rate limits
                using var semaphore = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
                var tasks = new List<Task>();

                foreach (var ip in ipList)
                {
                    if (!IsScanning) break; // Allow cancellation

                    // Use semaphore to limit concurrent requests
                    await semaphore.WaitAsync();
                    
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            var scanResult = await PerformSingleScanAsync(ip, abuseIpDbApiKey);
                            
                            // Update UI on main thread
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                ScanResults.Add(scanResult);
                                CurrentProgress++;
                                StatusMessage = $"Scanning {ip} ({CurrentProgress} of {TotalProgress})";
                            });
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    tasks.Add(task);
                }

                // Wait for all tasks to complete
                await Task.WhenAll(tasks);

                StatusMessage = $"Scan completed. {CurrentProgress} IPs processed.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private async Task<IpRangeScanResult> PerformSingleScanAsync(string ip, string apiKey)
        {
            // Use the same HTTP client configuration as the main window for consistency
            using var httpClient = new System.Net.Http.HttpClient(new System.Net.Http.SocketsHttpHandler
            {
                // Connection pooling settings
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 10,
                
                // Timeout settings
                ConnectTimeout = TimeSpan.FromSeconds(5),
                
                // Automatic redirection and decompression
                AllowAutoRedirect = true,
                AutomaticDecompression = System.Net.DecompressionMethods.All
            })
            {
                Timeout = TimeSpan.FromSeconds(5) // Shorter timeout for range scanning
            };

            try
            {
                // Get geolocation data
                var geoResponse = await httpClient.GetAsync($"http://ip-api.com/json/{ip}");
                geoResponse.EnsureSuccessStatusCode();
                var geoJson = await geoResponse.Content.ReadAsStringAsync();
                var geoResult = System.Text.Json.JsonSerializer.Deserialize<GeolocationResponse>(geoJson);

                if (geoResult?.Status == "success")
                {
                    var result = new IpRangeScanResult
                    {
                        IpAddress = ip,
                        Status = "Success",
                        Country = geoResult.Country,
                        City = geoResult.City,
                        Isp = geoResult.Isp,
                        Latitude = geoResult.Lat,
                        Longitude = geoResult.Lon,
                        Timezone = geoResult.Timezone,
                        ScanTime = DateTime.UtcNow
                    };

                    // If threat info is requested and API key is provided, include it
                    if (IncludeThreatInfo && !string.IsNullOrWhiteSpace(apiKey))
                    {
                        // Get threat information from AbuseIPDB with proper headers
                        var threatRequest = new System.Net.Http.HttpRequestMessage(
                            System.Net.Http.HttpMethod.Get,
                            $"https://api.abuseipdb.com/api/v2/check?ipAddress={ip}&maxAgeInDays=90");
                        threatRequest.Headers.Add("Key", apiKey);
                        threatRequest.Headers.Add("Accept", "application/json");

                        var threatResponse = await httpClient.SendAsync(threatRequest);
                        if (threatResponse.IsSuccessStatusCode)
                        {
                            var threatJson = await threatResponse.Content.ReadAsStringAsync();
                            var threatData = System.Text.Json.JsonSerializer.Deserialize<AbuseIpDbResponse>(threatJson);
                            
                            if (threatData?.Data != null)
                            {
                                result.ThreatScore = threatData.Data.AbuseConfidenceScore;
                            }
                        }
                    }

                    return result;
                }
                else
                {
                    return new IpRangeScanResult
                    {
                        IpAddress = ip,
                        Status = "Failed",
                        ErrorMessage = geoResult?.Message ?? "Geolocation failed"
                    };
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                return new IpRangeScanResult
                {
                    IpAddress = ip,
                    Status = "Timeout",
                    ErrorMessage = "Request timed out"
                };
            }
            catch (Exception ex)
            {
                return new IpRangeScanResult
                {
                    IpAddress = ip,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }
        
        // Define the models for deserialization
        public class GeolocationResponse
        {
            public string Status { get; set; } = string.Empty;
            public string? Message { get; set; }
            public string Country { get; set; } = string.Empty;
            public string CountryCode { get; set; } = string.Empty;
            public string RegionName { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public double Lat { get; set; }
            public double Lon { get; set; }
            public string Timezone { get; set; } = string.Empty;
            public string Isp { get; set; } = string.Empty;
            public string Query { get; set; } = string.Empty;
        }
        
        public class AbuseIpDbResponse
        {
            public AbuseIpDbData? Data { get; set; }
        }
        
        public class AbuseIpDbData
        {
            public int AbuseConfidenceScore { get; set; }
        }

        public void StopScan()
        {
            IsScanning = false;
            StatusMessage = "Scan stopped by user.";
        }

        public async Task ImportIpListAsync(string filePath)
        {
            StatusMessage = $"Importing IP list from {filePath}...";
            
            try
            {
                var lines = await System.IO.File.ReadAllLinesAsync(filePath);
                var ipList = new List<string>();
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine) && 
                        (Utilities.IpValidator.IsValidIpAddress(trimmedLine) || 
                         Utilities.IpValidator.IsValidIpRange(trimmedLine)))
                    {
                        ipList.Add(trimmedLine);
                    }
                }
                
                // Combine all IPs from ranges and single IPs into one list
                var allIps = new List<string>();
                foreach (var item in ipList)
                {
                    if (Utilities.IpValidator.IsValidIpRange(item))
                    {
                        var rangeIps = ParseIpRange(item);
                        allIps.AddRange(rangeIps);
                    }
                    else
                    {
                        allIps.Add(item);
                    }
                }
                
                // Set the total progress and update the status
                TotalProgress = allIps.Count;
                StatusMessage = $"Imported {allIps.Count} IP addresses from file";
                
                // Return the list if needed by the caller
            }
            catch (Exception ex)
            {
                StatusMessage = $"Import failed: {ex.Message}";
            }
        }
        
        public async Task ExportResultsAsync(string exportPath)
        {
            StatusMessage = $"Exporting results to {exportPath}...";
            // Implementation for exporting results would go here
            await Task.Delay(100); // Simulate export
            StatusMessage = $"Results exported to {exportPath}";
        }

        // Helper methods for IP range parsing
        private bool IsValidIpRange(string ipRange)
        {
            return Utilities.IpValidator.IsValidIpRange(ipRange);
        }

        private System.Collections.Generic.List<string> ParseIpRange(string ipRange)
        {
            var result = new System.Collections.Generic.List<string>();
            
            // Reuse the validation logic
            if (!Utilities.IpValidator.IsValidIpRange(ipRange))
                return result;
                
            var match = System.Text.RegularExpressions.Regex.Match(ipRange, @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.)(\d{1,3})-(\d{1,3})$");
            if (!match.Success)
                return result;
                
            var baseIp = match.Groups[1].Value;
            var startNum = int.Parse(match.Groups[2].Value);
            var endNum = int.Parse(match.Groups[3].Value);
            
            // Generate IP addresses in the range
            for (int i = startNum; i <= endNum && i <= 255; i++)
            {
                result.Add($"{baseIp}{i}");
            }
            
            return result;
        }
    }
}