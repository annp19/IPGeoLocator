using System;
using System.Collections.Generic;

namespace IPGeoLocator.NetworkTools.Utils
{
    public class SubnetInfo
    {
        public string NetworkAddress { get; set; } = "";
        public string BroadcastAddress { get; set; } = "";
        public string SubnetMask { get; set; } = "";
        public int Cidr { get; set; }
        public int HostCount { get; set; }
        public List<string> UsableIpRange { get; set; } = new();
        public string WildcardMask { get; set; } = "";
        public string NetworkClass { get; set; } = "";
        public string Status { get; set; } = "";
    }
}