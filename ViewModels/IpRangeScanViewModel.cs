using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.ViewModels
{
    public class IpRangeScanViewModel : INotifyPropertyChanged
    {
        private string _startIpAddress = "";
        private string _endIpAddress = "";
        private int _concurrentScans = 10;
        private bool _isScanning;
        private string _scanStatus = "Ready to scan IP range";
        private int _progress;
        private int _totalIps;
        private int _completedIps;
        private List<IpScanResult> _scanResults = new();

        public string StartIpAddress
        {
            get => _startIpAddress;
            set => SetProperty(ref _startIpAddress, value);
        }

        public string EndIpAddress
        {
            get => _endIpAddress;
            set => SetProperty(ref _endIpAddress, value);
        }

        public int ConcurrentScans
        {
            get => _concurrentScans;
            set => SetProperty(ref _concurrentScans, value);
        }

        public bool IsScanning
        {
            get => _isScanning;
            set => SetProperty(ref _isScanning, value);
        }

        public string ScanStatus
        {
            get => _scanStatus;
            set => SetProperty(ref _scanStatus, value);
        }

        public int Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        public int TotalIps
        {
            get => _totalIps;
            set => SetProperty(ref _totalIps, value);
        }

        public int CompletedIps
        {
            get => _completedIps;
            set => SetProperty(ref _completedIps, value);
        }

        public List<IpScanResult> ScanResults
        {
            get => _scanResults;
            set => SetProperty(ref _scanResults, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public async Task StartScanAsync(Func<string, Task<IpScanResult>> scanFunction, CancellationToken cancellationToken)
        {
            if (!ValidateInputs())
                return;

            if (!IPAddress.TryParse(StartIpAddress, out var startIp) || 
                !IPAddress.TryParse(EndIpAddress, out var endIp))
            {
                ScanStatus = "Invalid IP address format";
                return;
            }

            var ipList = GenerateIpRange(startIp, endIp);
            TotalIps = ipList.Count;
            CompletedIps = 0;
            Progress = 0;
            ScanResults.Clear();

            IsScanning = true;
            ScanStatus = $"Scanning {TotalIps} IP addresses...";

            try
            {
                await ProcessIpRangeAsync(ipList, scanFunction, cancellationToken);
                ScanStatus = $"Scan completed. Processed {CompletedIps}/{TotalIps} IP addresses.";
            }
            catch (OperationCanceledException)
            {
                ScanStatus = "Scan cancelled by user.";
            }
            catch (Exception ex)
            {
                ScanStatus = $"Scan failed: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(StartIpAddress))
            {
                ScanStatus = "Please enter a start IP address";
                return false;
            }

            if (string.IsNullOrWhiteSpace(EndIpAddress))
            {
                ScanStatus = "Please enter an end IP address";
                return false;
            }

            if (ConcurrentScans <= 0 || ConcurrentScans > 100)
            {
                ScanStatus = "Concurrent scans must be between 1 and 100";
                return false;
            }

            return true;
        }

        private List<string> GenerateIpRange(IPAddress startIp, IPAddress endIp)
        {
            var ips = new List<string>();
            var startBytes = startIp.GetAddressBytes();
            var endBytes = endIp.GetAddressBytes();

            // Convert to uint for easier manipulation
            Array.Reverse(startBytes);
            Array.Reverse(endBytes);
            var start = BitConverter.ToUInt32(startBytes, 0);
            var end = BitConverter.ToUInt32(endBytes, 0);

            // Generate range
            for (var i = start; i <= end; i++)
            {
                var bytes = BitConverter.GetBytes(i);
                Array.Reverse(bytes);
                ips.Add(new IPAddress(bytes).ToString());
            }

            return ips;
        }

        private async Task ProcessIpRangeAsync(List<string> ipList, Func<string, Task<IpScanResult>> scanFunction, CancellationToken cancellationToken)
        {
            var semaphore = new SemaphoreSlim(ConcurrentScans, ConcurrentScans);
            var tasks = new List<Task>();

            foreach (var ip in ipList)
            {
                // Wait for available slot
                await semaphore.WaitAsync(cancellationToken);
                
                if (cancellationToken.IsCancellationRequested)
                    break;

                var task = Task.Run(async () =>
                {
                    try
                    {
                        var result = await scanFunction(ip);
                        lock (ScanResults)
                        {
                            ScanResults.Add(result);
                            CompletedIps++;
                            Progress = (int)((double)CompletedIps / TotalIps * 100);
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorResult = new IpScanResult
                        {
                            IpAddress = ip,
                            Status = "Error",
                            ErrorMessage = ex.Message
                        };
                        
                        lock (ScanResults)
                        {
                            ScanResults.Add(errorResult);
                            CompletedIps++;
                            Progress = (int)((double)CompletedIps / TotalIps * 100);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }
    }

    public class IpScanResult
    {
        public string IpAddress { get; set; } = "";
        public string Status { get; set; } = "";
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public string Isp { get; set; } = "";
        public int ThreatScore { get; set; } = -1;
        public string ErrorMessage { get; set; } = "";
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
    }
}