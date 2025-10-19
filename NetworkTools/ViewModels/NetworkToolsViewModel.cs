using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NewtonsoftJson = Newtonsoft.Json;
using IPGeoLocator.NetworkTools.Scanners;
using IPGeoLocator.NetworkTools.Utils;

namespace IPGeoLocator.NetworkTools.ViewModels
{
    public class NetworkToolsViewModel : INotifyPropertyChanged
    {
        // Port Scanner Properties
        private string _targetIp = "";
        private int _startPort = 1;
        private int _endPort = 1000;
        private int _maxConcurrency = 100;
        private int _timeoutMs = 1000;
        private bool _isScanningPorts;
        private string _portScanStatus = "Ready to scan ports";
        private int _portScanProgress;
        private List<PortScanResult> _portScanResults = new();
        
        // Ping Properties
        private string _pingTarget = "";
        private int _pingCount = 4;
        private int _pingTimeoutMs = 5000;
        private int _pingIntervalMs = 1000;
        private bool _isPinging;
        private string _pingStatus = "Ready to ping";
        private int _pingProgress;
        private List<PingResult> _pingResults = new();
        
        // Traceroute Properties
        private string _traceTarget = "";
        private int _maxHops = 30;
        private int _traceTimeoutMs = 5000;
        private bool _isTracing;
        private string _traceStatus = "Ready to trace route";
        private int _traceProgress;
        private TracerouteResult _tracerouteResult = new();
        
        // Subnet Calculator Properties
        private string _subnetIpAddress = "";
        private string _subnetMask = "";
        private int _subnetCidr = 24;
        private SubnetInfo _subnetInfo = new();
        private bool _isCalculatingSubnet;
        private string _subnetStatus = "Ready to calculate subnet";
        
        // WHOIS Properties
        private string _whoisTarget = "";
        private bool _isLookingUpWhois;
        private string _whoisStatus = "Ready to lookup WHOIS";
        private WhoisResult _whoisResult = new();
        
        // DNS Properties
        private string _dnsTarget = "";
        private string _dnsRecordType = "A";
        private bool _isLookingUpDns;
        private string _dnsStatus = "Ready to lookup DNS";
        private DnsLookupResult _dnsResult = new();

        // Add a list of supported record types for the UI
        public List<string> DnsRecordTypes { get; } = Enum.GetNames(typeof(DnsClient.QueryType)).ToList();

        // Port Scanner Properties
        public string TargetIp 
        { 
            get => _targetIp; 
            set => SetProperty(ref _targetIp, value); 
        }
        
        public int StartPort 
        { 
            get => _startPort; 
            set => SetProperty(ref _startPort, value); 
        }
        
        public int EndPort 
        { 
            get => _endPort; 
            set => SetProperty(ref _endPort, value); 
        }
        
        public int MaxConcurrency 
        { 
            get => _maxConcurrency; 
            set => SetProperty(ref _maxConcurrency, value); 
        }
        
        public int TimeoutMs 
        { 
            get => _timeoutMs; 
            set => SetProperty(ref _timeoutMs, value); 
        }
        
        public bool IsScanningPorts 
        { 
            get => _isScanningPorts; 
            set => SetProperty(ref _isScanningPorts, value); 
        }
        
        public string PortScanStatus 
        { 
            get => _portScanStatus; 
            set => SetProperty(ref _portScanStatus, value); 
        }
        
        public int PortScanProgress 
        { 
            get => _portScanProgress; 
            set => SetProperty(ref _portScanProgress, value); 
        }
        
        public List<PortScanResult> PortScanResults 
        { 
            get => _portScanResults; 
            set => SetProperty(ref _portScanResults, value); 
        }
        
        // Ping Properties
        public string PingTarget 
        { 
            get => _pingTarget; 
            set => SetProperty(ref _pingTarget, value); 
        }
        
        public int PingCount 
        { 
            get => _pingCount; 
            set => SetProperty(ref _pingCount, value); 
        }
        
        public int PingTimeoutMs 
        { 
            get => _pingTimeoutMs; 
            set => SetProperty(ref _pingTimeoutMs, value); 
        }
        
        public int PingIntervalMs 
        { 
            get => _pingIntervalMs; 
            set => SetProperty(ref _pingIntervalMs, value); 
        }
        
        public bool IsPinging 
        { 
            get => _isPinging; 
            set => SetProperty(ref _isPinging, value); 
        }
        
        public string PingStatus 
        { 
            get => _pingStatus; 
            set => SetProperty(ref _pingStatus, value); 
        }
        
        public int PingProgress 
        { 
            get => _pingProgress; 
            set => SetProperty(ref _pingProgress, value); 
        }
        
        public List<PingResult> PingResults 
        { 
            get => _pingResults; 
            set => SetProperty(ref _pingResults, value); 
        }
        
        // Traceroute Properties
        public string TraceTarget 
        { 
            get => _traceTarget; 
            set => SetProperty(ref _traceTarget, value); 
        }
        
        public int MaxHops 
        { 
            get => _maxHops; 
            set => SetProperty(ref _maxHops, value); 
        }
        
        public int TraceTimeoutMs 
        { 
            get => _traceTimeoutMs; 
            set => SetProperty(ref _traceTimeoutMs, value); 
        }
        
        public bool IsTracing 
        { 
            get => _isTracing; 
            set => SetProperty(ref _isTracing, value); 
        }
        
        public string TraceStatus 
        { 
            get => _traceStatus; 
            set => SetProperty(ref _traceStatus, value); 
        }
        
        public int TraceProgress 
        { 
            get => _traceProgress; 
            set => SetProperty(ref _traceProgress, value); 
        }
        
        public TracerouteResult TracerouteResult 
        { 
            get => _tracerouteResult; 
            set => SetProperty(ref _tracerouteResult, value); 
        }
        
        // Subnet Calculator Properties
        public string SubnetIpAddress 
        { 
            get => _subnetIpAddress; 
            set => SetProperty(ref _subnetIpAddress, value); 
        }
        
        public string SubnetMask 
        { 
            get => _subnetMask; 
            set => SetProperty(ref _subnetMask, value); 
        }
        
        public int SubnetCidr 
        { 
            get => _subnetCidr; 
            set => SetProperty(ref _subnetCidr, value); 
        }
        
        public SubnetInfo SubnetInfo 
        { 
            get => _subnetInfo; 
            set => SetProperty(ref _subnetInfo, value); 
        }
        
        public bool IsCalculatingSubnet 
        { 
            get => _isCalculatingSubnet; 
            set => SetProperty(ref _isCalculatingSubnet, value); 
        }
        
        public string SubnetStatus 
        { 
            get => _subnetStatus; 
            set => SetProperty(ref _subnetStatus, value); 
        }
        
        // WHOIS Properties
        public string WhoisTarget 
        { 
            get => _whoisTarget; 
            set => SetProperty(ref _whoisTarget, value); 
        }
        
        public bool IsLookingUpWhois 
        { 
            get => _isLookingUpWhois; 
            set => SetProperty(ref _isLookingUpWhois, value); 
        }
        
        public string WhoisStatus 
        { 
            get => _whoisStatus; 
            set => SetProperty(ref _whoisStatus, value); 
        }
        
        public WhoisResult WhoisResult 
        { 
            get => _whoisResult; 
            set => SetProperty(ref _whoisResult, value); 
        }
        
        // DNS Properties
        public string DnsTarget 
        { 
            get => _dnsTarget; 
            set => SetProperty(ref _dnsTarget, value); 
        }
        
        public string DnsRecordType 
        { 
            get => _dnsRecordType; 
            set => SetProperty(ref _dnsRecordType, value); 
        }
        
        public bool IsLookingUpDns 
        { 
            get => _isLookingUpDns; 
            set => SetProperty(ref _isLookingUpDns, value); 
        }
        
        public string DnsStatus 
        { 
            get => _dnsStatus; 
            set => SetProperty(ref _dnsStatus, value); 
        }
        
        public DnsLookupResult DnsResult 
        { 
            get => _dnsResult; 
            set => SetProperty(ref _dnsResult, value); 
        }

        // INotifyPropertyChanged Implementation
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

        // Port Scanner Command
        public async Task ScanPortsCommand()
        {
            if (string.IsNullOrWhiteSpace(TargetIp))
            {
                PortScanStatus = "Please enter a target IP address or hostname";
                return;
            }

            if (!IPAddress.TryParse(TargetIp, out _) && !IsValidHostname(TargetIp))
            {
                PortScanStatus = "Invalid IP address or hostname format";
                return;
            }

            IsScanningPorts = true;
            PortScanStatus = $"Scanning ports {StartPort}-{EndPort} on {TargetIp}...";
            PortScanProgress = 0;
            PortScanResults.Clear();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Overall timeout
            var progress = new Progress<int>(value => PortScanProgress = value);

            try
            {
                var scanner = new PortScanner();
                var results = await scanner.ScanPortsAsync(
                    TargetIp, 
                    StartPort, 
                    EndPort, 
                    MaxConcurrency, 
                    TimeoutMs, 
                    progress, 
                    cts.Token);

                PortScanResults = results;
                PortScanStatus = $"Port scan completed. Found {results.Count} open ports.";
            }
            catch (OperationCanceledException)
            {
                PortScanStatus = "Port scan timed out.";
            }
            catch (Exception ex)
            {
                PortScanStatus = $"Port scan failed: {ex.Message}";
            }
            finally
            {
                IsScanningPorts = false;
                cts.Dispose();
            }
        }

        // Ping Command
        public async Task PingCommand()
        {
            if (string.IsNullOrWhiteSpace(PingTarget))
            {
                PingStatus = "Please enter a target IP address or hostname";
                return;
            }

            if (!IPAddress.TryParse(PingTarget, out _) && !IsValidHostname(PingTarget))
            {
                PingStatus = "Invalid IP address or hostname format";
                return;
            }

            IsPinging = true;
            PingStatus = $"Pinging {PingTarget} {PingCount} times...";
            PingProgress = 0;
            PingResults.Clear();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Overall timeout
            var progress = new Progress<int>(value => PingProgress = value);

            try
            {
                var pinger = new Pinger();
                var results = await pinger.PingMultipleAsync(
                    PingTarget, 
                    PingCount, 
                    PingTimeoutMs, 
                    PingIntervalMs, 
                    progress, 
                    cts.Token);

                PingResults = results;
                var successCount = results.Count(r => r.Status == IPStatus.Success);
                PingStatus = $"Ping completed. {successCount}/{results.Count} successful pings.";
            }
            catch (OperationCanceledException)
            {
                PingStatus = "Ping timed out.";
            }
            catch (Exception ex)
            {
                PingStatus = $"Ping failed: {ex.Message}";
            }
            finally
            {
                IsPinging = false;
                cts.Dispose();
            }
        }

        // Traceroute Command
        public async Task TraceRouteCommand()
        {
            if (string.IsNullOrWhiteSpace(TraceTarget))
            {
                TraceStatus = "Please enter a target IP address or hostname";
                return;
            }

            if (!IPAddress.TryParse(TraceTarget, out _) && !IsValidHostname(TraceTarget))
            {
                TraceStatus = "Invalid IP address or hostname format";
                return;
            }

            IsTracing = true;
            TraceStatus = $"Tracing route to {TraceTarget}...";
            TraceProgress = 0;
            TracerouteResult = new TracerouteResult { Target = TraceTarget };

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Overall timeout
            var progress = new Progress<int>(value => TraceProgress = value);

            try
            {
                var tracer = new Tracerouter();
                var result = await tracer.TraceAsync(
                    TraceTarget, 
                    MaxHops, 
                    TraceTimeoutMs, 
                    progress, 
                    cts.Token);

                TracerouteResult = result;
                TraceStatus = result.Success ? 
                    $"Traceroute completed successfully with {result.Hops.Count} hops." :
                    "Traceroute completed with errors.";
            }
            catch (OperationCanceledException)
            {
                TraceStatus = "Traceroute timed out.";
            }
            catch (Exception ex)
            {
                TraceStatus = $"Traceroute failed: {ex.Message}";
            }
            finally
            {
                IsTracing = false;
                cts.Dispose();
            }
        }

        // Subnet Calculator Command
        public void CalculateSubnetCommand()
        {
            if (string.IsNullOrWhiteSpace(SubnetIpAddress))
            {
                SubnetStatus = "Please enter an IP address";
                return;
            }

            if (!IPAddress.TryParse(SubnetIpAddress, out _))
            {
                SubnetStatus = "Invalid IP address format";
                return;
            }

            IsCalculatingSubnet = true;
            SubnetStatus = "Calculating subnet...";

            try
            {
                SubnetInfo info;
                
                if (!string.IsNullOrWhiteSpace(SubnetMask))
                {
                    if (!IPAddress.TryParse(SubnetMask, out _))
                    {
                        SubnetStatus = "Invalid subnet mask format";
                        return;
                    }
                    info = SubnetCalculator.CalculateSubnet(SubnetIpAddress, SubnetMask);
                }
                else
                {
                    if (SubnetCidr < 0 || SubnetCidr > 32)
                    {
                        SubnetStatus = "CIDR must be between 0 and 32";
                        return;
                    }
                    info = SubnetCalculator.CalculateSubnetFromCidr(SubnetIpAddress, SubnetCidr);
                }

                SubnetInfo = info;
                SubnetStatus = "Subnet calculation completed successfully.";
            }
            catch (Exception ex)
            {
                SubnetStatus = $"Subnet calculation failed: {ex.Message}";
            }
            finally
            {
                IsCalculatingSubnet = false;
            }
        }

        // WHOIS Lookup Command
        public async Task LookupWhoisCommand()
        {
            if (string.IsNullOrWhiteSpace(WhoisTarget))
            {
                WhoisStatus = "Please enter a domain name or IP address";
                return;
            }

            IsLookingUpWhois = true;
            WhoisStatus = $"Looking up WHOIS information for {WhoisTarget}...";

            try
            {
                var whois = new WhoisLookup();
                
                WhoisResult = await whois.LookupDomainAsync(WhoisTarget);
                WhoisStatus = "WHOIS lookup completed successfully.";
            }
            catch (Exception ex)
            {
                WhoisStatus = $"WHOIS lookup failed: {ex.Message}";
            }
            finally
            {
                IsLookingUpWhois = false;
            }
        }

        // DNS Lookup Command
        public async Task LookupDnsCommand()
        {
            if (string.IsNullOrWhiteSpace(DnsTarget))
            {
                DnsStatus = "Please enter a domain name or IP address";
                return;
            }

            IsLookingUpDns = true;
            DnsStatus = $"Looking up DNS records for {DnsTarget}...";

            try
            {
                var dns = new DnsLookup();
                if (Enum.TryParse<DnsClient.QueryType>(DnsRecordType, true, out var queryType))
                {
                    DnsResult = await dns.LookupAsync(DnsTarget, queryType);
                }
                else
                {
                    DnsStatus = "Invalid record type selected.";
                }
                DnsStatus = DnsResult.Status;
            }
            catch (Exception ex)
            {
                DnsStatus = $"DNS lookup failed: {ex.Message}";
            }
            finally
            {
                IsLookingUpDns = false;
            }
        }

        // Helper methods
        private bool IsValidHostname(string hostname)
        {
            try
            {
                var uri = new Uri($"http://{hostname}");
                return uri.Host == hostname;
            }
            catch
            {
                return false;
            }
        }
    }
}