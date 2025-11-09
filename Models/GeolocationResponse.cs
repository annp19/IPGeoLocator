using System.Text.Json.Serialization;

namespace IPGeoLocator.Models;

public record GeolocationResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("country")] string Country,
    [property: JsonPropertyName("countryCode")] string CountryCode,
    [property: JsonPropertyName("regionName")] string RegionName,
    [property: JsonPropertyName("city")] string City,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("isp")] string Isp,
    [property: JsonPropertyName("query")] string Query
);