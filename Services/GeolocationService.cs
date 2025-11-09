using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using IPGeoLocator.Models;

namespace IPGeoLocator.Services;

/// <summary>
/// Service for handling geolocation API calls with caching
/// </summary>
public class GeolocationService
{
    private readonly HttpClient _httpClient;
    private readonly CacheService<string, GeolocationResponse> _geoCache;
    private readonly CacheService<string, byte[]> _flagCache;
    private readonly CacheService<string, string> _timeCache;

    public GeolocationService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _geoCache = new CacheService<string, GeolocationResponse>(1000, TimeSpan.FromMinutes(15));
        _flagCache = new CacheService<string, byte[]>(1000, TimeSpan.FromMinutes(30));
        _timeCache = new CacheService<string, string>(1000, TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Get geolocation information for an IP address
    /// </summary>
    public async Task<GeolocationResponse?> GetGeolocationAsync(string ipAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new ArgumentException("IP address cannot be null or empty", nameof(ipAddress));

        return await _geoCache.GetOrAddAsync(
            ipAddress,
            async () => await FetchGeolocationAsync(ipAddress, cancellationToken).ConfigureAwait(false)
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Get country flag image bytes
    /// </summary>
    public async Task<Bitmap?> GetCountryFlagAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return null;

        var flagBytes = await _flagCache.GetOrAddAsync(
            countryCode.ToLower(),
            async () => await FetchFlagAsync(countryCode, cancellationToken).ConfigureAwait(false)
        ).ConfigureAwait(false);

        if (flagBytes != null && flagBytes.Length > 0)
        {
            try
            {
                return new Bitmap(new MemoryStream(flagBytes));
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Get local time for coordinates with fallback strategies
    /// </summary>
    public async Task<string> GetLocalTimeAsync(
        double latitude, 
        double longitude, 
        string timezone, 
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{latitude:F4},{longitude:F4},{timezone}";
        
        return await _timeCache.GetOrAddAsync(
            cacheKey,
            async () => await FetchLocalTimeAsync(latitude, longitude, timezone, cancellationToken).ConfigureAwait(false),
            TimeSpan.FromMinutes(5) // Shorter expiration for time data
        ).ConfigureAwait(false) ?? "N/A";
    }

    /// <summary>
    /// Get user's public IP address
    /// </summary>
    public async Task<string?> GetMyIpAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            
            return await _httpClient.GetStringAsync("https://api.ipify.org", cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get IP: {ex.Message}");
            return null;
        }
    }

    // Private helper methods

    private async Task<GeolocationResponse?> FetchGeolocationAsync(string ipAddress, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"http://ip-api.com/json/{ipAddress}", 
                cancellationToken
            ).ConfigureAwait(false);
            
            response.EnsureSuccessStatusCode();
            
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<GeolocationResponse>(json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Geolocation lookup failed: {ex.Message}");
            return null;
        }
    }

    private async Task<byte[]?> FetchFlagAsync(string countryCode, CancellationToken cancellationToken)
    {
        try
        {
            return await _httpClient.GetByteArrayAsync(
                $"https://flagcdn.com/32x24/{countryCode.ToLower()}.png",
                cancellationToken
            ).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> FetchLocalTimeAsync(
        double latitude, 
        double longitude, 
        string timezone, 
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        // Try timeapi.io first (fastest)
        try
        {
            var url = $"https://timeapi.io/api/Time/current/coordinate?latitude={latitude}&longitude={longitude}";
            var response = await _httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var timeData = JsonSerializer.Deserialize<TimeApiResponse>(json);
                if (timeData != null)
                    return timeData.DateTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        catch { /* Try fallback */ }

        // Try worldtimeapi.org as fallback
        if (!string.IsNullOrWhiteSpace(timezone))
        {
            try
            {
                var url = $"https://worldtimeapi.org/api/timezone/{timezone}";
                var response = await _httpClient.GetAsync(url, cts.Token).ConfigureAwait(false);
                
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("datetime", out var dtProp))
                    {
                        var dtStr = dtProp.GetString();
                        if (DateTime.TryParse(dtStr, out var dt))
                            return dt.ToString("yyyy-MM-dd HH:mm:ss");
                    }
                }
            }
            catch { /* Use fallback */ }
        }

        // Last fallback: UTC time
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
    }

    /// <summary>
    /// Clear all caches
    /// </summary>
    public void ClearCaches()
    {
        _geoCache.Clear();
        _flagCache.Clear();
        _timeCache.Clear();
    }
}

// Response models
public record TimeApiResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("dateTime")] DateTime DateTime
);
