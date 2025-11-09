using System.Threading;
using System.Threading.Tasks;
using IPGeoLocator.Services;

namespace IPGeoLocator.Services
{
    public static class ThreatIntelligenceServiceExtensions
    {
        // Add an extension method to mimic GetThreatInfoAsync pattern expected by ViewModel
        public static async Task<ThreatInfoViewModel> GetThreatInfoAsync(
            this ThreatIntelligenceService service, 
            string ipAddress, 
            string abuseIpDbApiKey, 
            string virusTotalApiKey, 
            CancellationToken cancellationToken)
        {
            // Compose settings from passed-in keys
            var settings = new ThreatIntelSettings
            {
                EnableAbuseIPDB = !string.IsNullOrWhiteSpace(abuseIpDbApiKey),
                AbuseIPDBApiKey = abuseIpDbApiKey,
                EnableVirusTotal = !string.IsNullOrWhiteSpace(virusTotalApiKey),
                VirusTotalApiKey = virusTotalApiKey
            };

            // Call main aggregation method
            var aggregate = await service.GetAggregatedThreatInfoAsync(ipAddress, settings);
            // Compose a legacy viewmodel with desired properties (add conversion if missing)
            return new ThreatInfoViewModel
            {
                Summary = $"Score {aggregate.AverageConfidenceScore}, Any Threat: {aggregate.IsMalicious}",
                Score = aggregate.AverageConfidenceScore,
                AbuseIpDbInfo = string.Join("; ", aggregate.IndividualResults.FindAll(r=>r.ServiceName=="AbuseIPDB").ConvertAll(r => r.Status)),
                VirusTotalInfo = string.Join("; ", aggregate.IndividualResults.FindAll(r=>r.ServiceName=="VirusTotal").ConvertAll(r => r.Status))
            };
        }
    }

    // Helper transition viewmodel
    public class ThreatInfoViewModel
    {
        public string Summary { get; set; } = "";
        public int Score { get; set; }
        public string AbuseIpDbInfo { get; set; } = "";
        public string VirusTotalInfo { get; set; } = "";
    }
}
