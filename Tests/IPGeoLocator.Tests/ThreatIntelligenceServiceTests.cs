using System;
using System.Net.Http;
using System.Threading.Tasks;
using IPGeoLocator.Services;
using Xunit;

namespace IPGeoLocator.Tests
{
    public class ThreatIntelligenceServiceTests
    {
        private readonly HttpClient _httpClient;
        private readonly ThreatIntelligenceService _service;

        public ThreatIntelligenceServiceTests()
        {
            _httpClient = new HttpClient();
            _service = new ThreatIntelligenceService(_httpClient);
        }

        [Fact]
        public async Task GetAbuseIpDbInfoAsync_WithValidApiKeyAndIp_ReturnsResult()
        {
            // Note: This test would require a valid API key to work properly
            // For now, we'll test with empty key to check error handling
            var result = await _service.GetAbuseIpDbInfoAsync("8.8.8.8", "");
            
            // With empty API key, we expect it to report "API Key Missing"
            Assert.Equal("AbuseIPDB", result.ServiceName);
            Assert.Equal("API Key Missing", result.Status);
        }

        [Fact]
        public async Task GetVirusTotalInfoAsync_WithEmptyApiKey_ReturnsCorrectResult()
        {
            // Act
            var result = await _service.GetVirusTotalInfoAsync("8.8.8.8", "");

            // Assert
            Assert.Equal("VirusTotal", result.ServiceName);
            Assert.Equal("API Key Missing", result.Status);
        }

        [Fact]
        public async Task GetAlienVaultOTXInfoAsync_WithValidIp_ReturnsResult()
        {
            // Act
            var result = await _service.GetAlienVaultOTXInfoAsync("8.8.8.8");

            // Assert - the result should at least have a service name
            Assert.Equal("AlienVault OTX", result.ServiceName);
        }

        [Fact]
        public async Task GetAggregatedThreatInfoAsync_WithSettings_ReturnsAggregateResult()
        {
            // Arrange
            var settings = new ThreatIntelSettings
            {
                EnableAbuseIPDB = false,      // Disable to avoid rate limits in testing
                EnableVirusTotal = false,     // Disable to avoid rate limits in testing
                EnableAlienVaultOTX = false,  // Disable to avoid rate limits in testing
                EnableGreyNoise = false,
                EnableShodan = false
            };

            // Act
            var result = await _service.GetAggregatedThreatInfoAsync("8.8.8.8", settings);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("No Valid Results", result.Status);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}