using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace IPGeoLocator.Services
{
    public interface INetworkToolsService
    {
        Task<NetworkToolResult> PingAsync(string target);
        Task<NetworkToolResult> TracerouteAsync(string target);
        Task<NetworkToolResult> WhoisAsync(string target);
    }

    public class NetworkToolsService : INetworkToolsService
    {
        public async Task<NetworkToolResult> PingAsync(string target)
        {
            try
            {
                // Use system ping command - this approach works cross-platform with Process
                var processInfo = new ProcessStartInfo
                {
                    FileName = GetPingCommand(),
                    Arguments = GetPingArguments(target),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return new NetworkToolResult { Success = false, Output = "Failed to start ping process" };

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                var result = new NetworkToolResult
                {
                    Success = process.ExitCode == 0,
                    Output = string.IsNullOrEmpty(output) ? error : output,
                    ExitCode = process.ExitCode
                };

                return result;
            }
            catch (Exception ex)
            {
                return new NetworkToolResult { Success = false, Output = $"Ping error: {ex.Message}" };
            }
        }

        public async Task<NetworkToolResult> TracerouteAsync(string target)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = GetTracerouteCommand(),
                    Arguments = target,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return new NetworkToolResult { Success = false, Output = "Failed to start traceroute process" };

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                var result = new NetworkToolResult
                {
                    Success = process.ExitCode == 0,
                    Output = string.IsNullOrEmpty(output) ? error : output,
                    ExitCode = process.ExitCode
                };

                return result;
            }
            catch (Exception ex)
            {
                return new NetworkToolResult { Success = false, Output = $"Traceroute error: {ex.Message}" };
            }
        }

        public async Task<NetworkToolResult> WhoisAsync(string target)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "whois",
                    Arguments = target,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                    return new NetworkToolResult { Success = false, Output = "Failed to start whois process" };

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                var result = new NetworkToolResult
                {
                    Success = process.ExitCode == 0,
                    Output = string.IsNullOrEmpty(output) ? error : output,
                    ExitCode = process.ExitCode
                };

                return result;
            }
            catch (Exception ex)
            {
                return new NetworkToolResult { Success = false, Output = $"Whois error: {ex.Message}" };
            }
        }

        private string GetPingCommand()
        {
            // Use 'ping' on Unix-like systems and 'ping.exe' on Windows (though 'ping' also works on Windows)
            return IsWindows() ? "ping" : "ping";
        }

        private string GetPingArguments(string target)
        {
            // Cross-platform ping arguments
            // On Windows: ping -n 4 target
            // On Unix/Linux/macOS: ping -c 4 target
            return IsWindows() ? $"-n 4 {target}" : $"-c 4 {target}";
        }

        private string GetTracerouteCommand()
        {
            // Use 'traceroute' on Unix/Linux/macOS and 'tracert' on Windows
            return IsWindows() ? "tracert" : "traceroute";
        }

        private bool IsWindows()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
        }
    }

    public class NetworkToolResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public int ExitCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}