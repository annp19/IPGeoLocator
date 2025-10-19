using System;
using System.Collections.Generic;
using System.Net;

namespace IPGeoLocator.NetworkTools.Utils
{
    public class SubnetCalculator
    {
        public static SubnetInfo CalculateSubnet(string ipAddress, string subnetMask)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException("Invalid IP address format");

            if (!IPAddress.TryParse(subnetMask, out var mask))
                throw new ArgumentException("Invalid subnet mask format");

            var ipBytes = ip.GetAddressBytes();
            var maskBytes = mask.GetAddressBytes();

            // Calculate network address
            var networkBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                networkBytes[i] = (byte)(ipBytes[i] & maskBytes[i]);
            }
            var networkAddress = new IPAddress(networkBytes);

            // Calculate broadcast address
            var broadcastBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                broadcastBytes[i] = (byte)(ipBytes[i] | (maskBytes[i] ^ 255));
            }
            var broadcastAddress = new IPAddress(broadcastBytes);

            // Calculate CIDR notation
            int cidr = 0;
            for (int i = 0; i < 4; i++)
            {
                cidr += Convert.ToString(maskBytes[i], 2).Replace("0", "").Length;
            }

            // Calculate wildcard mask
            var wildcardBytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                wildcardBytes[i] = (byte)(maskBytes[i] ^ 255);
            }
            var wildcardMask = new IPAddress(wildcardBytes);

            // Calculate host count
            int hostBits = 32 - cidr;
            int hostCount = hostBits > 0 ? (int)Math.Pow(2, hostBits) - 2 : 0;

            // Calculate usable IP range (first and last few IPs)
            var usableIps = new List<string>();
            if (hostCount > 0)
            {
                var firstUsable = IncrementIpAddress(networkAddress, 1);
                var lastUsable = DecrementIpAddress(broadcastAddress, 1);
                
                usableIps.Add(firstUsable.ToString());
                if (hostCount > 2)
                    usableIps.Add("...");
                if (hostCount > 1)
                    usableIps.Add(lastUsable.ToString());
            }

            // Determine network class
            string networkClass = DetermineNetworkClass(ipBytes[0]);

            return new SubnetInfo
            {
                NetworkAddress = networkAddress.ToString(),
                BroadcastAddress = broadcastAddress.ToString(),
                SubnetMask = mask.ToString(),
                Cidr = cidr,
                HostCount = hostCount,
                UsableIpRange = usableIps,
                WildcardMask = wildcardMask.ToString(),
                NetworkClass = networkClass,
                Status = "Success"
            };
        }

        public static SubnetInfo CalculateSubnetFromCidr(string ipAddress, int cidr)
        {
            if (cidr < 0 || cidr > 32)
                throw new ArgumentException("CIDR must be between 0 and 32");

            var maskBytes = new byte[4];
            for (int i = 0; i < cidr; i++)
            {
                maskBytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
            }
            var subnetMask = new IPAddress(maskBytes);

            return CalculateSubnet(ipAddress, subnetMask.ToString());
        }

        private static IPAddress IncrementIpAddress(IPAddress address, int increment)
        {
            var bytes = address.GetAddressBytes();
            Array.Reverse(bytes); // Convert to little-endian for arithmetic
            ulong ip = BitConverter.ToUInt32(bytes, 0);
            ip += (uint)increment;
            bytes = BitConverter.GetBytes(ip);
            Array.Reverse(bytes); // Convert back to big-endian
            return new IPAddress(bytes);
        }

        private static IPAddress DecrementIpAddress(IPAddress address, int decrement)
        {
            var bytes = address.GetAddressBytes();
            Array.Reverse(bytes); // Convert to little-endian for arithmetic
            ulong ip = BitConverter.ToUInt32(bytes, 0);
            ip -= (uint)decrement;
            bytes = BitConverter.GetBytes(ip);
            Array.Reverse(bytes); // Convert back to big-endian
            return new IPAddress(bytes);
        }

        private static string DetermineNetworkClass(byte firstOctet)
        {
            return firstOctet switch
            {
                <= 127 => "Class A",
                <= 191 => "Class B",
                <= 223 => "Class C",
                <= 239 => "Class D (Multicast)",
                _ => "Class E (Experimental)"
            };
        }
    }
}