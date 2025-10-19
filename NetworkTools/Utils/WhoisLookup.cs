using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace IPGeoLocator.NetworkTools.Utils
{
    public class WhoisResult
    {
        public string Domain { get; set; } = "";
        public string Registrar { get; set; } = "";
        public string CreationDate { get; set; } = "";
        public string ExpirationDate { get; set; } = "";
        public string UpdatedDate { get; set; } = "";
        public string NameServers { get; set; } = "";
        public string Status { get; set; } = "";
        public string Owner { get; set; } = "";
        public string OwnerOrganization { get; set; } = "";
        public string OwnerAddress { get; set; } = "";
        public string OwnerCity { get; set; } = "";
        public string OwnerState { get; set; } = "";
        public string OwnerPostalCode { get; set; } = "";
        public string OwnerCountry { get; set; } = "";
        public string AdminContact { get; set; } = "";
        public string TechContact { get; set; } = "";
        public string RawData { get; set; } = "";
        public DateTime LookupTime { get; set; } = DateTime.UtcNow;
    }

    public class WhoisLookup
    {
        private static readonly HttpClient HttpClient = new HttpClient();

        public async Task<WhoisResult> LookupDomainAsync(
            string domain, 
            CancellationToken cancellationToken = default)
        {
            var result = new WhoisResult
            {
                Domain = domain
            };

            try
            {
                // Using a free WHOIS API (there are several options available)
                // Note: For production use, you might want to use a paid API service
                var url = $"https://whoisjson.com/api/v1/whois?domain={domain}";
                
                // Alternative public WHOIS APIs:
                // var url = $"https://www.whoisxmlapi.com/whoisserver/WhoisService?apiKey=YOUR_API_KEY&domainName={domain}&outputFormat=JSON";
                // var url = $"https://api.whoapi.com/?domain={domain}&r=whois&apikey=YOUR_API_KEY";
                
                var response = await HttpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var whoisData = JsonSerializer.Deserialize<JsonElement>(json);
                
                // Parse common WHOIS fields (this will vary based on the API used)
                if (whoisData.TryGetProperty("registrar", out var registrar))
                    result.Registrar = registrar.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("created_date", out var createdDate))
                    result.CreationDate = createdDate.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("expires_date", out var expiresDate))
                    result.ExpirationDate = expiresDate.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("updated_date", out var updatedDate))
                    result.UpdatedDate = updatedDate.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("nameservers", out var nameServers))
                    result.NameServers = nameServers.GetRawText();
                    
                if (whoisData.TryGetProperty("status", out var status))
                    result.Status = status.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("registrant", out var registrant))
                {
                    if (registrant.TryGetProperty("name", out var name))
                        result.Owner = name.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("organization", out var org))
                        result.OwnerOrganization = org.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("street", out var street))
                        result.OwnerAddress = street.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("city", out var city))
                        result.OwnerCity = city.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("state", out var state))
                        result.OwnerState = state.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("postal_code", out var postalCode))
                        result.OwnerPostalCode = postalCode.GetString() ?? "";
                        
                    if (registrant.TryGetProperty("country", out var country))
                        result.OwnerCountry = country.GetString() ?? "";
                }
                
                result.RawData = json;
            }
            catch (Exception ex)
            {
                result.Status = $"Error: {ex.Message}";
            }

            return result;
        }

        public async Task<WhoisResult> LookupIpAsync(
            string ipAddress, 
            CancellationToken cancellationToken = default)
        {
            var result = new WhoisResult
            {
                Domain = ipAddress
            };

            try
            {
                // Using a free IP WHOIS API
                var url = $"https://whoisjson.com/api/v1/whois?ip={ipAddress}";
                
                var response = await HttpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var whoisData = JsonSerializer.Deserialize<JsonElement>(json);
                
                // Parse IP WHOIS fields
                if (whoisData.TryGetProperty("asn", out var asn))
                    result.Registrar = $"ASN: {asn.GetString() ?? "N/A"}";
                    
                if (whoisData.TryGetProperty("org", out var org))
                    result.OwnerOrganization = org.GetString() ?? "";
                    
                if (whoisData.TryGetProperty("net_range", out var netRange))
                    result.OwnerAddress = netRange.GetString() ?? "";
                    
                result.RawData = json;
            }
            catch (Exception ex)
            {
                result.Status = $"Error: {ex.Message}";
            }

            return result;
        }
    }
}