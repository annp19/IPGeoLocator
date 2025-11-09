using System;
using System.Net;
using System.Text.RegularExpressions;

namespace IPGeoLocator.Utilities
{
    public static class IpValidator
    {
        // Regular expressions for different IP formats
        private static readonly Regex IpRangeRegex = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.)(\d{1,3})-(\d{1,3})$");
        private static readonly Regex CidrRegex = new Regex(@"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})\/(\d{1,2})$");
        
        /// <summary>
        /// Validates if the input string is a valid IP address (IPv4 or IPv6)
        /// </summary>
        /// <param name="input">The input string to validate</param>
        /// <returns>True if the input is a valid IP address, false otherwise</returns>
        public static bool IsValidIpAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;
                
            return IPAddress.TryParse(input.Trim(), out _);
        }
        
        /// <summary>
        /// Validates if the input is a valid IP range (e.g., 192.168.1.1-100)
        /// </summary>
        /// <param name="input">The input string to validate</param>
        /// <returns>True if the input is a valid IP range, false otherwise</returns>
        public static bool IsValidIpRange(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;
                
            var match = IpRangeRegex.Match(input.Trim());
            if (!match.Success)
                return false;
                
            var baseIp = match.Groups[1].Value;
            var startNum = int.Parse(match.Groups[2].Value);
            var endNum = int.Parse(match.Groups[3].Value);
            
            // Check if start is less than or equal to end
            if (startNum > endNum)
                return false;
                
            // Check if range is reasonable (not too large)
            if (endNum - startNum > 255) // Max range is one octet
                return false;
                
            // Validate the base IP part
            var fullIp = baseIp + startNum;
            return IsValidIpAddress(fullIp);
        }
        
        /// <summary>
        /// Validates if the input is a valid CIDR notation (e.g., 192.168.1.0/24)
        /// </summary>
        /// <param name="input">The input string to validate</param>
        /// <returns>True if the input is a valid CIDR, false otherwise</returns>
        public static bool IsValidCidr(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;
                
            var match = CidrRegex.Match(input.Trim());
            if (!match.Success)
                return false;
                
            var ipAddress = match.Groups[1].Value;
            var prefixLength = int.Parse(match.Groups[2].Value);
            
            // Validate the IP part
            if (!IsValidIpAddress(ipAddress))
                return false;
                
            // Validate the prefix length (0-32 for IPv4)
            return prefixLength >= 0 && prefixLength <= 32;
        }
        
        /// <summary>
        /// Validates the input as a general IP query (single IP, range, or CIDR)
        /// </summary>
        /// <param name="input">The input string to validate</param>
        /// <returns>A validation result with details</returns>
        public static IpValidationResult ValidateIpInput(string input)
        {
            var result = new IpValidationResult { Input = input };
            
            if (string.IsNullOrWhiteSpace(input))
            {
                result.IsValid = false;
                result.Error = "Input is empty";
                result.Type = IpInputType.Unknown;
                return result;
            }
            
            input = input.Trim();
            
            // Check for single IP address
            if (IsValidIpAddress(input))
            {
                result.IsValid = true;
                result.Type = IpInputType.SingleIp;
                result.Normalized = input;
                return result;
            }
            
            // Check for IP range
            if (IsValidIpRange(input))
            {
                result.IsValid = true;
                result.Type = IpInputType.IpRange;
                result.Normalized = input;
                return result;
            }
            
            // Check for CIDR notation
            if (IsValidCidr(input))
            {
                result.IsValid = true;
                result.Type = IpInputType.Cidr;
                result.Normalized = input;
                return result;
            }
            
            // If none of the above, it's invalid
            result.IsValid = false;
            result.Error = "Invalid IP format. Supported formats: single IP (1.2.3.4), range (1.2.3.1-100), or CIDR (1.2.3.0/24)";
            result.Type = IpInputType.Unknown;
            return result;
        }
        
        /// <summary>
        /// Checks if an IP address is a private IP address
        /// </summary>
        /// <param name="ipAddress">The IP address to check</param>
        /// <returns>True if the IP is private, false otherwise</returns>
        public static bool IsPrivateIpAddress(string ipAddress)
        {
            if (!IsValidIpAddress(ipAddress))
                return false;
                
            var ip = IPAddress.Parse(ipAddress);
            
            // Check for IPv6
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                // IPv6 unique local addresses (fc00::/7)
                return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || 
                       ipAddress.StartsWith("fc", StringComparison.OrdinalIgnoreCase) ||
                       ipAddress.StartsWith("fd", StringComparison.OrdinalIgnoreCase);
            }
            
            // IPv4 private ranges
            var bytes = ip.GetAddressBytes();
            switch (bytes[0])
            {
                case 10: // 10.0.0.0/8
                    return true;
                case 172: // 172.16.0.0/12
                    return bytes[1] >= 16 && bytes[1] <= 31;
                case 192: // 192.168.0.0/16
                    return bytes[1] == 168;
                case 127: // 127.0.0.0/8 (loopback)
                    return true;
                case 169: // 169.254.0.0/16 (link-local)
                    return bytes[1] == 254;
                default:
                    return false;
            }
        }
    }
    
    public enum IpInputType
    {
        Unknown,
        SingleIp,
        IpRange,
        Cidr
    }
    
    public class IpValidationResult
    {
        public string Input { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Error { get; set; }
        public IpInputType Type { get; set; }
        public string? Normalized { get; set; }
    }
}