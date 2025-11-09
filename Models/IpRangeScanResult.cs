using System;

namespace IPGeoLocator.Models
{
    public class IpRangeScanResult
    {
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // Success, Failed, Error
        public string? Country { get; set; }
        public string? City { get; set; }
        public string? Isp { get; set; }
        public int? ThreatScore { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.UtcNow;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Timezone { get; set; }
    }
}