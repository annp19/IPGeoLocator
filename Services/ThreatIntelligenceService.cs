using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace IPGeoLocator.Services
{
    public interface IThreatIntelligenceService
    {
        Task<ThreatIntelResult> GetAbuseIpDbInfoAsync(string ipAddress, string apiKey);
        Task<ThreatIntelResult> GetVirusTotalInfoAsync(string ipAddress, string apiKey);
        Task<ThreatIntelResult> GetAlienVaultOTXInfoAsync(string ipAddress);
        Task<ThreatIntelResult> GetGreyNoiseInfoAsync(string ipAddress, string apiKey);
        Task<ThreatIntelResult> GetShodanInfoAsync(string ipAddress, string apiKey);
        Task<ThreatIntelResult> GetHybridAnalysisInfoAsync(string ipAddress, string apiKey);
        Task<AggregateThreatResult> GetAggregatedThreatInfoAsync(string ipAddress, ThreatIntelSettings settings);
    }

    public class ThreatIntelligenceService : IThreatIntelligenceService
    {
        private readonly HttpClient _httpClient;

        public ThreatIntelligenceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<ThreatIntelResult> GetAbuseIpDbInfoAsync(string ipAddress, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ThreatIntelResult { ServiceName = "AbuseIPDB", Status = "API Key Missing" };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, 
                    $"https://api.abuseipdb.com/api/v2/check?ipAddress={ipAddress}&maxAgeInDays=90");
                request.Headers.Add("Key", apiKey);
                request.Headers.Add("Accept", "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new ThreatIntelResult { ServiceName = "AbuseIPDB", Status = $"HTTP {response.StatusCode}" };

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<AbuseIpDbResponse>(json);

                if (data?.Data == null)
                    return new ThreatIntelResult { ServiceName = "AbuseIPDB", Status = "No Data" };

                return new ThreatIntelResult
                {
                    ServiceName = "AbuseIPDB",
                    ConfidenceScore = data.Data.AbuseConfidenceScore,
                    IsMalicious = data.Data.AbuseConfidenceScore > 50,
                    LastReportedAt = data.Data.LastReportedAt,
                    TotalReports = data.Data.TotalReports,
                    Status = "Success"
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "AbuseIPDB", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<ThreatIntelResult> GetVirusTotalInfoAsync(string ipAddress, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ThreatIntelResult { ServiceName = "VirusTotal", Status = "API Key Missing" };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://www.virustotal.com/vtapi/v2/ip-address/report?apikey={apiKey}&ip={ipAddress}");
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new ThreatIntelResult { ServiceName = "VirusTotal", Status = $"HTTP {response.StatusCode}" };

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<VirusTotalResponse>(json);

                if (data?.ResponseCode != 1)
                    return new ThreatIntelResult { ServiceName = "VirusTotal", Status = "No Data" };

                var maliciousCount = 0;
                if (data.DetectedUrls != null)
                    maliciousCount += data.DetectedUrls.Count;

                return new ThreatIntelResult
                {
                    ServiceName = "VirusTotal",
                    ConfidenceScore = maliciousCount > 0 ? Math.Min(100, maliciousCount * 10) : 0,
                    IsMalicious = maliciousCount > 0,
                    Status = "Success"
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "VirusTotal", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<ThreatIntelResult> GetAlienVaultOTXInfoAsync(string ipAddress)
        {
            try
            {
                var response = await _httpClient.GetAsync($"https://otx.alienvault.com/api/v1/indicators/IPv4/{ipAddress}/general");
                if (!response.IsSuccessStatusCode)
                    return new ThreatIntelResult { ServiceName = "AlienVault OTX", Status = $"HTTP {response.StatusCode}" };

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<AlienVaultOTXResponse>(json);

                var pulseCount = data?.PulseInfo?.Count ?? 0;
                var isMalicious = pulseCount > 0;

                return new ThreatIntelResult
                {
                    ServiceName = "AlienVault OTX",
                    ConfidenceScore = isMalicious ? Math.Min(100, pulseCount * 20) : 0,
                    IsMalicious = isMalicious,
                    Status = "Success"
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "AlienVault OTX", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<ThreatIntelResult> GetGreyNoiseInfoAsync(string ipAddress, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ThreatIntelResult { ServiceName = "GreyNoise", Status = "API Key Missing" };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get,
                    $"https://api.greynoise.io/v3/community/{ipAddress}");
                request.Headers.Add("key", apiKey);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // GreyNoise returns 404 for non-noisy IPs, which is actually a good thing
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        return new ThreatIntelResult
                        {
                            ServiceName = "GreyNoise",
                            ConfidenceScore = 0,
                            IsMalicious = false,
                            Status = "Clean (No Malicious Activity)"
                        };
                    }
                    return new ThreatIntelResult { ServiceName = "GreyNoise", Status = $"HTTP {response.StatusCode}" };
                }

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<GreyNoiseResponse>(json);

                var isMalicious = data?.Classification == "malicious";
                var confidence = isMalicious ? 90 : (data?.Classification == "benign" ? 10 : 50);

                return new ThreatIntelResult
                {
                    ServiceName = "GreyNoise",
                    ConfidenceScore = confidence,
                    IsMalicious = isMalicious,
                    Status = "Success"
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "GreyNoise", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<ThreatIntelResult> GetShodanInfoAsync(string ipAddress, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ThreatIntelResult { ServiceName = "Shodan", Status = "API Key Missing" };

            try
            {
                var response = await _httpClient.GetAsync($"https://api.shodan.io/shodan/host/{ipAddress}?key={apiKey}");
                if (!response.IsSuccessStatusCode)
                    return new ThreatIntelResult { ServiceName = "Shodan", Status = $"HTTP {response.StatusCode}" };

                var json = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<ShodanResponse>(json);

                var isOpenPort = data?.Ports?.Length > 0;
                // Shodan detects open ports, which isn't necessarily malicious but indicates the host is active
                var confidence = isOpenPort ? 30 : 0;

                return new ThreatIntelResult
                {
                    ServiceName = "Shodan",
                    ConfidenceScore = confidence,
                    IsMalicious = false, // Shodan identifies services, not necessarily malicious activity
                    Status = "Success",
                    AdditionalInfo = isOpenPort ? $"{data.Ports.Length} open ports detected" : "No open ports detected"
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "Shodan", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<ThreatIntelResult> GetHybridAnalysisInfoAsync(string ipAddress, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new ThreatIntelResult { ServiceName = "Hybrid-Analysis", Status = "API Key Missing" };

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.hybrid-analysis.com/api/v2/search/terms");
                request.Headers.Add("api-key", apiKey);
                request.Headers.Add("user-agent", "Falcon Sandbox");
                request.Content = new StringContent($"{ipAddress}", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return new ThreatIntelResult { ServiceName = "Hybrid-Analysis", Status = $"HTTP {response.StatusCode}" };

                // Hybrid-Analysis doesn't directly search by IP, but we can check if there are any submissions
                var json = await response.Content.ReadAsStringAsync();
                // In a real implementation, we would parse the response and determine threat level
                
                return new ThreatIntelResult
                {
                    ServiceName = "Hybrid-Analysis",
                    Status = "Not Implemented", // Simplified for this example
                    ConfidenceScore = 0
                };
            }
            catch (Exception ex)
            {
                return new ThreatIntelResult { ServiceName = "Hybrid-Analysis", Status = $"Error: {ex.Message}" };
            }
        }

        public async Task<AggregateThreatResult> GetAggregatedThreatInfoAsync(string ipAddress, ThreatIntelSettings settings)
        {
            var tasks = new List<Task<ThreatIntelResult>>();

            if (settings.EnableAbuseIPDB)
                tasks.Add(GetAbuseIpDbInfoAsync(ipAddress, settings.AbuseIPDBApiKey));

            if (settings.EnableVirusTotal)
                tasks.Add(GetVirusTotalInfoAsync(ipAddress, settings.VirusTotalApiKey));

            if (settings.EnableAlienVaultOTX)
                tasks.Add(GetAlienVaultOTXInfoAsync(ipAddress));

            if (settings.EnableGreyNoise)
                tasks.Add(GetGreyNoiseInfoAsync(ipAddress, settings.GreyNoiseApiKey));

            if (settings.EnableShodan)
                tasks.Add(GetShodanInfoAsync(ipAddress, settings.ShodanApiKey));

            var results = await Task.WhenAll(tasks);
            return CalculateAggregateScore(results);
        }

        private AggregateThreatResult CalculateAggregateScore(ThreatIntelResult[] results)
        {
            var validResults = new List<ThreatIntelResult>();
            var totalScore = 0;
            var maliciousCount = 0;

            foreach (var result in results)
            {
                if (result.Status == "Success")
                {
                    validResults.Add(result);
                    totalScore += result.ConfidenceScore;
                    if (result.IsMalicious)
                        maliciousCount++;
                }
            }

            var averageScore = validResults.Count > 0 ? totalScore / validResults.Count : 0;
            var isMalicious = maliciousCount > validResults.Count / 2; // Majority vote

            return new AggregateThreatResult
            {
                AverageConfidenceScore = averageScore,
                IsMalicious = isMalicious,
                IndividualResults = validResults,
                Status = validResults.Count > 0 ? "Success" : "No Valid Results"
            };
        }
    }

    public class ThreatIntelSettings
    {
        public bool EnableAbuseIPDB { get; set; } = true;
        public string AbuseIPDBApiKey { get; set; } = "";
        
        public bool EnableVirusTotal { get; set; } = false;
        public string VirusTotalApiKey { get; set; } = "";
        
        public bool EnableAlienVaultOTX { get; set; } = true;
        public bool EnableGreyNoise { get; set; } = false;
        public string GreyNoiseApiKey { get; set; } = "";
        
        public bool EnableShodan { get; set; } = false;
        public string ShodanApiKey { get; set; } = "";
        
        public bool EnableHybridAnalysis { get; set; } = false;
        public string HybridAnalysisApiKey { get; set; } = "";
    }

    public class ThreatIntelResult
    {
        public string ServiceName { get; set; } = "";
        public int ConfidenceScore { get; set; }
        public bool IsMalicious { get; set; }
        public string Status { get; set; } = "";
        public DateTime? LastReportedAt { get; set; }
        public int TotalReports { get; set; }
        public string AdditionalInfo { get; set; } = "";
    }

    public class AggregateThreatResult
    {
        public int AverageConfidenceScore { get; set; }
        public bool IsMalicious { get; set; }
        public List<ThreatIntelResult> IndividualResults { get; set; } = new();
        public string Status { get; set; } = "";
    }

    // Response models for various threat intel services
    public class AbuseIpDbResponse
    {
        public AbuseIpDbData? Data { get; set; }
    }

    public class AbuseIpDbData
    {
        public int AbuseConfidenceScore { get; set; }
        public DateTime? LastReportedAt { get; set; }
        public int TotalReports { get; set; }
    }

    public class VirusTotalResponse
    {
        public int ResponseCode { get; set; }
        public List<VirusTotalDetectedUrl>? DetectedUrls { get; set; }
    }

    public class VirusTotalDetectedUrl
    {
        public string Url { get; set; } = "";
        public int Positives { get; set; }
        public int Total { get; set; }
    }

    public class AlienVaultOTXResponse
    {
        public AlienVaultPulseInfo? PulseInfo { get; set; }
    }

    public class AlienVaultPulseInfo
    {
        public int Count { get; set; }
    }

    public class GreyNoiseResponse
    {
        public string Classification { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ShodanResponse
    {
        public int[]? Ports { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
    }
}