using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.NetworkTools.Scanners
{
    public class PortScanResult
    {
        public int Port { get; set; }
        public string Status { get; set; } = "Unknown";
        public string Service { get; set; } = "Unknown";
        public long ResponseTimeMs { get; set; } = -1;
    }

    public class PortScanner
    {
        private static readonly Dictionary<int, string> WellKnownPorts = new()
        {
            { 21, "FTP" },
            { 22, "SSH" },
            { 23, "Telnet" },
            { 25, "SMTP" },
            { 53, "DNS" },
            { 80, "HTTP" },
            { 110, "POP3" },
            { 143, "IMAP" },
            { 443, "HTTPS" },
            { 993, "IMAPS" },
            { 995, "POP3S" },
            { 1433, "MSSQL" },
            { 3306, "MySQL" },
            { 3389, "RDP" },
            { 5432, "PostgreSQL" },
            { 5900, "VNC" },
            { 8080, "HTTP Proxy" },
            { 8443, "HTTPS Alt" }
        };

        public static string DetectService(int port)
        {
            return WellKnownPorts.TryGetValue(port, out var service) ? service : "Unknown";
        }

        public async Task<List<PortScanResult>> ScanPortsAsync(
            string target, 
            int startPort, 
            int endPort, 
            int maxConcurrency = 100,
            int timeoutMs = 1000,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<PortScanResult>();
            var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = new List<Task<PortScanResult>>();
            int totalPorts = endPort - startPort + 1;
            int completedPorts = 0;

            for (int port = startPort; port <= endPort; port++)
            {
                var portCopy = port; // Capture for closure
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await ScanSinglePortAsync(target, portCopy, timeoutMs, cancellationToken);
                        
                        // Update progress
                        Interlocked.Increment(ref completedPorts);
                        progress?.Report((int)((double)completedPorts / totalPorts * 100));
                        
                        return result;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Wait for all tasks to complete
            var scanResults = await Task.WhenAll(tasks);
            
            // Filter out closed ports and add to results
            foreach (var result in scanResults)
            {
                if (result.Status == "Open")
                {
                    results.Add(result);
                }
            }

            return results;
        }

        private async Task<PortScanResult> ScanSinglePortAsync(
            string target, 
            int port, 
            int timeoutMs, 
            CancellationToken cancellationToken)
        {
            var result = new PortScanResult
            {
                Port = port,
                Service = DetectService(port)
            };

            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                await client.ConnectAsync(target, port).WaitAsync(cts.Token);
                stopwatch.Stop();

                result.Status = "Open";
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }
            catch (OperationCanceledException)
            {
                result.Status = "Filtered";
            }
            catch
            {
                result.Status = "Closed";
            }

            return result;
        }
    }
}