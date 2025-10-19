using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.NetworkTools.Scanners
{
    public class TracerouteHop
    {
        public int HopNumber { get; set; }
        public string Address { get; set; } = "";
        public long RoundTripTimeMs { get; set; } = -1;
        public IPStatus Status { get; set; } = IPStatus.Unknown;
        public string StatusMessage { get; set; } = "";
        public bool IsTarget { get; set; } = false;
    }

    public class TracerouteResult
    {
        public string Target { get; set; } = "";
        public List<TracerouteHop> Hops { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Success { get; set; } = false;
    }

    public class Tracerouter
    {
        public async Task<TracerouteResult> TraceAsync(
            string target, 
            int maxHops = 30, 
            int timeoutMs = 5000,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new TracerouteResult
            {
                Target = target
            };

            try
            {
                // Resolve target hostname to IP if needed
                IPAddress targetIp;
                if (!IPAddress.TryParse(target, out targetIp))
                {
                    var hostEntry = await Dns.GetHostEntryAsync(target, cancellationToken);
                    if (hostEntry.AddressList.Length > 0)
                    {
                        IPAddress? firstIp = hostEntry.AddressList[0];
                        if (firstIp != null)
                        {
                            targetIp = firstIp;
                        }
                        else
                        {
                            throw new Exception($"DNS resolved to a null IP address for {target}");
                        }
                    }
                    else
                    {
                        throw new Exception($"Could not resolve host: {target}");
                    }
                }

                using var ping = new Ping();
                
                for (int ttl = 1; ttl <= maxHops; ttl++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var hop = new TracerouteHop
                    {
                        HopNumber = ttl
                    };

                    // Send ping with specific TTL
                    var options = new PingOptions(ttl, true);
                    var buffer = new byte[32]; // Standard payload size
                    
                    try
                    {
                        var reply = await ping.SendPingAsync(targetIp, timeoutMs, buffer, options);
                        hop.Status = reply.Status;
                        hop.StatusMessage = GetStatusMessage(reply.Status);
                        
                        if (reply.Status == IPStatus.Success || reply.Status == IPStatus.TtlExpired)
                        {
                            hop.Address = reply.Address?.ToString() ?? "Unknown";
                            hop.RoundTripTimeMs = reply.RoundtripTime;
                            
                            // Check if we've reached the target
                            if (reply.Status == IPStatus.Success)
                            {
                                hop.IsTarget = true;
                                result.Hops.Add(hop);
                                result.Success = true;
                                break;
                            }
                        }
                        else
                        {
                            hop.Address = "*";
                        }
                    }
                    catch (Exception ex)
                    {
                        hop.Status = IPStatus.Unknown;
                        hop.StatusMessage = $"Error: {ex.Message}";
                        hop.Address = "*";
                    }

                    result.Hops.Add(hop);
                    
                    // Report progress
                    progress?.Report((int)((double)ttl / maxHops * 100));
                }
            }
            catch (Exception ex)
            {
                // Handle general errors
                result.Hops.Add(new TracerouteHop
                {
                    HopNumber = 0,
                    Address = "Error",
                    StatusMessage = ex.Message,
                    Status = IPStatus.Unknown
                });
            }

            return result;
        }

        private string GetStatusMessage(IPStatus status)
        {
            return status switch
            {
                IPStatus.Success => "Success",
                IPStatus.TtlExpired => "TTL Expired",
                IPStatus.TimedOut => "Timed Out",
                IPStatus.DestinationHostUnreachable => "Host Unreachable",
                IPStatus.DestinationNetworkUnreachable => "Network Unreachable",
                IPStatus.DestinationPortUnreachable => "Port Unreachable",
                IPStatus.PacketTooBig => "Packet Too Big",
                IPStatus.BadOption => "Bad Option",
                IPStatus.BadRoute => "Bad Route",
                IPStatus.TtlReassemblyTimeExceeded => "Reassembly Time Exceeded",
                IPStatus.ParameterProblem => "Parameter Problem",
                IPStatus.SourceQuench => "Source Quench",
                IPStatus.Unknown => "Unknown",
                _ => status.ToString()
            };
        }
    }
}