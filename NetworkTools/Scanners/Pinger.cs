using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.NetworkTools.Scanners
{
    public class PingResult
    {
        public string Target { get; set; } = "";
        public long RoundTripTimeMs { get; set; } = -1;
        public IPStatus Status { get; set; } = IPStatus.Unknown;
        public string StatusMessage { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class Pinger
    {
        public async Task<PingResult> PingAsync(string target, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            var result = new PingResult
            {
                Target = target
            };

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, timeoutMs);
                
                result.RoundTripTimeMs = reply.RoundtripTime;
                result.Status = reply.Status;
                result.StatusMessage = GetStatusMessage(reply.Status);
            }
            catch (Exception ex)
            {
                result.Status = IPStatus.Unknown;
                result.StatusMessage = $"Error: {ex.Message}";
            }

            return result;
        }

        public async Task<List<PingResult>> PingMultipleAsync(
            string target, 
            int count = 4, 
            int timeoutMs = 5000,
            int intervalMs = 1000,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var results = new List<PingResult>();
            
            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var result = await PingAsync(target, timeoutMs, cancellationToken);
                results.Add(result);
                
                // Report progress
                progress?.Report((int)((double)(i + 1) / count * 100));
                
                // Delay between pings (except for the last one)
                if (i < count - 1)
                {
                    await Task.Delay(intervalMs, cancellationToken);
                }
            }
            
            return results;
        }

        private string GetStatusMessage(IPStatus status)
        {
            return status switch
            {
                IPStatus.Success => "Success",
                IPStatus.TimedOut => "Timed Out",
                IPStatus.DestinationHostUnreachable => "Host Unreachable",
                IPStatus.DestinationNetworkUnreachable => "Network Unreachable",
                IPStatus.DestinationPortUnreachable => "Port Unreachable",
                IPStatus.PacketTooBig => "Packet Too Big",
                IPStatus.BadOption => "Bad Option",
                IPStatus.BadRoute => "Bad Route",
                IPStatus.TtlExpired => "TTL Expired",
                IPStatus.TtlReassemblyTimeExceeded => "Reassembly Time Exceeded",
                IPStatus.ParameterProblem => "Parameter Problem",
                IPStatus.SourceQuench => "Source Quench",
                IPStatus.Unknown => "Unknown",
                _ => status.ToString()
            };
        }
    }
}