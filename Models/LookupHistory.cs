using System;
using System.ComponentModel.DataAnnotations;

namespace IPGeoLocator.Models
{
    public class LookupHistory
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(45)] // IPv6 max length
        public string IpAddress { get; set; } = string.Empty;
        
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? RegionName { get; set; }
        public string? City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Timezone { get; set; }
        public string? Isp { get; set; }
        public string? Query { get; set; }
        public int? ThreatScore { get; set; }
        public string? ThreatInfo { get; set; }
        public DateTime LookupTime { get; set; } = DateTime.UtcNow;
        public string? LookupDuration { get; set; }
    }
}