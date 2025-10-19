using System;

namespace IPGeoLocator.Models
{
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