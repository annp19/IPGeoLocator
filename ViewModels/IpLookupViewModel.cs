using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using IPGeoLocator.Data;
using IPGeoLocator.Models;
using IPGeoLocator.Services;
using NewtonsoftJson = Newtonsoft.Json;
using System.Threading;
using System.Linq;
using Avalonia.Controls;
using System.Text.Json.Serialization;

namespace IPGeoLocator.ViewModels
{
    public class IpLookupViewModel : INotifyPropertyChanged
    {
        // HttpClient and caches
        private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static readonly Dictionary<string, (GeolocationResponse? response, DateTime timestamp)> _geoCache = new();
        private static readonly Dictionary<string, (Bitmap? bitmap, DateTime timestamp)> _flagCache = new();
        private static readonly Dictionary<string, (string time, DateTime timestamp)> _localTimeCache = new();
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(1);

        // Services and context
        private readonly AppDbContext _dbContext;
        private readonly LookupHistoryService _historyService;

        // UI State Properties
        private string _ipAddressInput = "";
        private string _abuseIpDbApiKey = "";
        private string _virusTotalApiKey = "";
        private string _statusMessage = "Enter an IP and click Lookup.";
        private bool _isLoading;
        private bool _isResultVisible;
        private ISolidColorBrush _statusBrush = Brushes.Gray;
        private GeolocationResponse? _geolocationResult;
        private string _localTime = "N/A";
        private string _lookupDuration = "";
        private Bitmap? _flagImage;
        private string _threatInfo = "N/A";
        private int _threatScore = -1;
        private string _abuseipdbInfo = "N/A";
        private string _virustotalInfo = "N/A";
        private string _threatCrowdInfo = "N/A";

        // Data-bound properties
        public string IpAddressInput { get => _ipAddressInput; set => SetProperty(ref _ipAddressInput, value); }
        public string AbuseIpDbApiKey { get => _abuseIpDbApiKey; set => SetProperty(ref _abuseIpDbApiKey, value); }
        public string VirusTotalApiKey { get => _virusTotalApiKey; set => SetProperty(ref _virusTotalApiKey, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
        public bool IsResultVisible { get => _isResultVisible; set => SetProperty(ref _isResultVisible, value); }
        public ISolidColorBrush StatusBrush { get => _statusBrush; set => SetProperty(ref _statusBrush, value); }
        public GeolocationResponse? GeolocationResult { get => _geolocationResult; set => SetProperty(ref _geolocationResult, value); }
        public string LocalTime { get => _localTime; set => SetProperty(ref _localTime, value); }
        public Bitmap? FlagImage { get => _flagImage; set => SetProperty(ref _flagImage, value); }
        public string LookupDuration { get => _lookupDuration; set => SetProperty(ref _lookupDuration, value); }
        public string ThreatInfo { get => _threatInfo; set => SetProperty(ref _threatInfo, value); }
        public int ThreatScore { get => _threatScore; set => SetProperty(ref _threatScore, value); }
        public string AbuseIpDbInfo { get => _abuseipdbInfo; set => SetProperty(ref _abuseipdbInfo, value); }
        public string VirusTotalInfo { get => _virustotalInfo; set => SetProperty(ref _virustotalInfo, value); }
        public string ThreatCrowdInfo { get => _threatCrowdInfo; set => SetProperty(ref _threatCrowdInfo, value); }

        // Computed properties
        public string LocationString => $"{GeolocationResult?.City}, {GeolocationResult?.RegionName}, {GeolocationResult?.Country}";
        public string CoordinatesString => $"{GeolocationResult?.Lat:F4}, {GeolocationResult?.Lon:F4}";

        public IpLookupViewModel()
        {
            _dbContext = new AppDbContext();
            _historyService = new LookupHistoryService(_dbContext);
        }

        public async Task GetMyIpCommand()
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                IpAddressInput = await HttpClient.GetStringAsync("https://api.ipify.org", cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                SetStatus($"Error: {ex.Message}", isError: true);
            }
        }

        public async Task LookupIpCommand()
        {
            if (!System.Net.IPAddress.TryParse(IpAddressInput, out _))
            {
                SetStatus("Invalid IP address format.", isError: true);
                return;
            }

            IsLoading = true;
            IsResultVisible = false;
            SetStatus("Fetching data...", isWorking: true);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                var geoTask = GetGeolocationAsync(IpAddressInput, cts.Token);
                var geoResult = await geoTask;
                GeolocationResult = geoResult;

                if (GeolocationResult?.Status != "success")
                {
                    SetStatus(GeolocationResult?.Message ?? "Unknown geolocation error", isError: true);
                    IsLoading = false;
                    LookupDuration = "";
                    return;
                }

                var timeTask = GetLocalTimeAsync(GeolocationResult.Lat, GeolocationResult.Lon, GeolocationResult.Timezone, cts.Token);
                var flagTask = GetCountryFlagAsync(GeolocationResult.CountryCode, cts.Token);
                var threatTask = GetThreatInfoAsync(GeolocationResult.Query, AbuseIpDbApiKey, cts.Token);

                using var secondaryCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                secondaryCts.CancelAfter(TimeSpan.FromSeconds(3));

                try
                {
                    await Task.WhenAll(
                        timeTask.ContinueWith(t => { }, secondaryCts.Token),
                        flagTask.ContinueWith(t => { }, secondaryCts.Token),
                        threatTask.ContinueWith(t => { }, secondaryCts.Token)
                    );
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("Secondary tasks timed out, continuing with primary data");
                }

                try { LocalTime = timeTask.IsCompletedSuccessfully ? timeTask.Result : "Unavailable"; } catch { LocalTime = "Unavailable"; }
                try { FlagImage = flagTask.IsCompletedSuccessfully ? flagTask.Result : null; } catch { FlagImage = null; }
                try
                {
                    if (threatTask.IsCompletedSuccessfully)
                    {
                        ThreatInfo = threatTask.Result;
                        ThreatScore = ParseThreatScore(ThreatInfo);
                    }
                }
                catch { ThreatInfo = "Unavailable"; }

                sw.Stop();
                LookupDuration = $"Lookup time: {sw.ElapsedMilliseconds} ms";

                IsResultVisible = true;
                SetStatus("Lookup successful!", isSuccess: true);

                _ = Task.Run(async () =>
                {
                    try { await SaveLookupToHistoryAsync(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to save to history: {ex.Message}"); }
                });
            }
            catch (Exception ex)
            {
                IsResultVisible = false;
                SetStatus($"Error: {ex.Message}", isError: true);
                LookupDuration = "";
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task CopyAllCommand(TopLevel topLevel)
        {
            if (GeolocationResult == null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"IP Address: {GeolocationResult.Query}");
            sb.AppendLine($"Location: {LocationString}");
            sb.AppendLine($"ISP: {GeolocationResult.Isp}");
            sb.AppendLine($"Coordinates: {CoordinatesString}");
            sb.AppendLine($"Timezone: {GeolocationResult.Timezone}");
            sb.AppendLine($"Local Time: {LocalTime}");
            await (topLevel.Clipboard?.SetTextAsync(sb.ToString()) ?? Task.CompletedTask);
            SetStatus("Copied to clipboard!", isSuccess: true);
        }

        public async Task CopyCoordsCommand(TopLevel topLevel)
        {
            if (GeolocationResult == null) return;
            await (topLevel.Clipboard?.SetTextAsync(CoordinatesString) ?? Task.CompletedTask);
            SetStatus("Copied to clipboard!", isSuccess: true);
        }

        public async Task ExportResultsCommand(TopLevel topLevel)
        {
            if (GeolocationResult == null)
            {
                SetStatus("No results to export.", isError: true);
                return;
            }

            var filePickerSaveOptions = new FilePickerSaveOptions
            {
                Title = "Export Results",
                SuggestedFileName = $"ip_details_{GeolocationResult.Query}",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
                    new FilePickerFileType("CSV") { Patterns = new[] { "*.csv" } },
                    new FilePickerFileType("Text File") { Patterns = new[] { "*.txt" } }
                },
                DefaultExtension = "json"
            };

            try
            {
                var file = await topLevel.StorageProvider.SaveFilePickerAsync(filePickerSaveOptions);
                if (file is not null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await using var writer = new StreamWriter(stream, Encoding.UTF8);
                    string extension = Path.GetExtension(file.Name).ToLowerInvariant();
                    string content = string.Empty;
                    switch (extension)
                    {
                        case ".json": content = GetJsonContent(); break;
                        case ".csv": content = GetCsvContent(); break;
                        case ".txt": content = GetTxtContent(); break;
                        default: SetStatus($"Unsupported file format: {extension}", isError: true); return;
                    }
                    await writer.WriteAsync(content);
                    SetStatus($"Results exported to: {file.Name}", isSuccess: true);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"Export failed: {ex.Message}", isError: true);
            }
        }

        // Helper and private methods
        private string GetJsonContent()
        {
            var exportData = new
            {
                Query = GeolocationResult?.Query,
                Status = GeolocationResult?.Status,
                Country = GeolocationResult?.Country,
                CountryCode = GeolocationResult?.CountryCode,
                RegionName = GeolocationResult?.RegionName,
                City = GeolocationResult?.City,
                Latitude = GeolocationResult?.Lat,
                Longitude = GeolocationResult?.Lon,
                Timezone = GeolocationResult?.Timezone,
                Isp = GeolocationResult?.Isp,
                LookupTime = DateTime.UtcNow,
                LocalTime = LocalTime,
                ThreatScore = ThreatScore,
                ThreatInfo = ThreatInfo
            };
            return NewtonsoftJson.JsonConvert.SerializeObject(exportData, NewtonsoftJson.Formatting.Indented);
        }

        private string GetCsvContent()
        {
            var csv = new StringBuilder();
            csv.AppendLine("Field,Value");
            csv.AppendLine($"IP Address,\"{GeolocationResult?.Query ?? ""}\"");
            csv.AppendLine($"Status,\"{GeolocationResult?.Status ?? ""}\"");
            csv.AppendLine($"Country,\"{GeolocationResult?.Country ?? ""}\"");
            csv.AppendLine($"Country Code,\"{GeolocationResult?.CountryCode ?? ""}\"");
            csv.AppendLine($"Region,\"{GeolocationResult?.RegionName ?? ""}\"");
            csv.AppendLine($"City,\"{GeolocationResult?.City ?? ""}\"");
            csv.AppendLine($"Latitude,\"{GeolocationResult?.Lat}\"");
            csv.AppendLine($"Longitude,\"{GeolocationResult?.Lon}\"");
            csv.AppendLine($"Timezone,\"{GeolocationResult?.Timezone ?? ""}\"");
            csv.AppendLine($"ISP,\"{GeolocationResult?.Isp ?? ""}\"");
            csv.AppendLine($"Lookup Time,\"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\"");
            csv.AppendLine($"Local Time,\"{LocalTime}\"");
            csv.AppendLine($"Threat Score,\"{ThreatScore}\"");
            csv.AppendLine($"Threat Info,\"{ThreatInfo}\"");
            return csv.ToString();
        }

        private string GetTxtContent()
        {
            var txt = new StringBuilder();
            txt.AppendLine("IP GEOLOCATION RESULTS");
            txt.AppendLine("=====================");
            txt.AppendLine($"IP Address: {GeolocationResult?.Query ?? "N/A"}");
            txt.AppendLine($"Status: {GeolocationResult?.Status ?? "N/A"}");
            txt.AppendLine($"Country: {GeolocationResult?.Country ?? "N/A"}");
            txt.AppendLine($"Country Code: {GeolocationResult?.CountryCode ?? "N/A"}");
            txt.AppendLine($"Region: {GeolocationResult?.RegionName ?? "N/A"}");
            txt.AppendLine($"City: {GeolocationResult?.City ?? "N/A"}");
            txt.AppendLine($"Coordinates: {CoordinatesString}");
            txt.AppendLine($"Timezone: {GeolocationResult?.Timezone ?? "N/A"}");
            txt.AppendLine($"ISP: {GeolocationResult?.Isp ?? "N/A"}");
            txt.AppendLine($"Lookup Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
            txt.AppendLine($"Local Time: {LocalTime}");
            txt.AppendLine($"Threat Score: {ThreatScore}");
            txt.AppendLine($"Threat Info: {ThreatInfo}");
            return txt.ToString();
        }

        private async Task<GeolocationResponse?> GetGeolocationAsync(string ip, CancellationToken token)
        {
            if (_geoCache.TryGetValue(ip, out var cached) && DateTime.Now - cached.timestamp < CacheExpiration)
                return cached.response;
            try
            {
                var response = await HttpClient.GetAsync($"http://ip-api.com/json/{ip}", token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<GeolocationResponse>(json);
                _geoCache[ip] = (result, DateTime.Now);
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Geolocation lookup failed: {ex.Message}");
                return null;
            }
        }

        private async Task<Bitmap?> GetCountryFlagAsync(string countryCode, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(countryCode)) return null;
            if (_flagCache.TryGetValue(countryCode, out var cached) && DateTime.Now - cached.timestamp < CacheExpiration && cached.bitmap != null)
                return cached.bitmap;
            try
            {
                var data = await HttpClient.GetByteArrayAsync($"https://flagcdn.com/32x24/{countryCode.ToLower()}.png", token).ConfigureAwait(false);
                var bmp = new Bitmap(new MemoryStream(data));
                _flagCache[countryCode] = (bmp, DateTime.Now);
                return bmp;
            }
            catch
            {
                _flagCache[countryCode] = (null, DateTime.Now);
                return null;
            }
        }

        private int ParseThreatScore(string threatInfo)
        {
            if (string.IsNullOrWhiteSpace(threatInfo)) return -1;
            var digits = System.Text.RegularExpressions.Regex.Match(threatInfo, @"\d+");
            if (digits.Success && int.TryParse(digits.Value, out int score))
                return score;
            return -1;
        }

        private async Task<string> GetThreatInfoAsync(string ip, string apiKey, CancellationToken token)
        {
            var threatTasks = new List<Task<string>>();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                threatTasks.Add(GetAbuseIpDbInfoAsync(ip, apiKey, token));
            }
            else
            {
                AbuseIpDbInfo = "API key not provided.";
            }
            if (!string.IsNullOrWhiteSpace(VirusTotalApiKey))
            {
                threatTasks.Add(GetVirusTotalInfoAsync(ip, VirusTotalApiKey, token));
            }
            else
            {
                VirusTotalInfo = "API key not provided.";
            }
            ThreatCrowdInfo = "N/A - Service not implemented";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(TimeSpan.FromSeconds(4));

            try
            {
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
                var allTasks = Task.WhenAll(threatTasks);
                var completedTask = await Task.WhenAny(allTasks, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    var completedResults = threatTasks
                        .Where(t => t.IsCompletedSuccessfully)
                        .Select(t => t.Result)
                        .Where(r => !string.IsNullOrEmpty(r) && r != "N/A" && r != "API key not provided.")
                        .ToList();
                    return completedResults.Any() ? string.Join("; ", completedResults) : "Threat check timed out.";
                }

                var results = await allTasks;
                var allResults = results
                    .Where(r => !string.IsNullOrEmpty(r) && r != "N/A" && r != "API key not provided.")
                    .ToList();
                return allResults.Any() ? string.Join("; ", allResults) : "No threat data found.";
            }
            catch (OperationCanceledException)
            {
                return "Threat check cancelled.";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Threat intelligence error: {ex.Message}");
                return "Error retrieving threat data.";
            }
        }

        private async Task<string> GetAbuseIpDbInfoAsync(string ip, string apiKey, CancellationToken token)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.abuseipdb.com/api/v2/check?ipAddress={ip}&maxAgeInDays=90");
                req.Headers.Add("Key", apiKey);
                req.Headers.Add("Accept", "application/json");
                var resp = await HttpClient.SendAsync(req, token);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = $"AbuseIPDB error: {resp.StatusCode}";
                    AbuseIpDbInfo = errorMsg;
                    return errorMsg;
                }
                var json = await resp.Content.ReadAsStringAsync(token);
                var data = JsonSerializer.Deserialize<AbuseIpDbResponse>(json);
                if (data?.Data == null)
                {
                    AbuseIpDbInfo = "No threat data.";
                    return "No threat data.";
                }
                int score = data.Data.AbuseConfidenceScore;
                string scoreText;
                if (score < 30) scoreText = $"Clean (Score {score})";
                else if (score < 80) scoreText = $"Suspicious (Score {score})";
                else scoreText = $"Dangerous (Score {score})";
                AbuseIpDbInfo = scoreText;
                return scoreText;
            }
            catch (Exception ex)
            {
                var error = $"AbuseIPDB error: {ex.Message}";
                AbuseIpDbInfo = error;
                return error;
            }
        }

        private async Task<string> GetVirusTotalInfoAsync(string ip, string apiKey, CancellationToken token)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://www.virustotal.com/vtapi/v2/ip-address/report?apikey={apiKey}&ip={ip}");
                var resp = await HttpClient.SendAsync(req, token);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorMsg = $"VirusTotal error: {resp.StatusCode}";
                    VirusTotalInfo = errorMsg;
                    return errorMsg;
                }
                var json = await resp.Content.ReadAsStringAsync(token);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response_code", out var responseCode) && responseCode.GetInt32() == 1)
                {
                    string result = "No specific threats";
                    if (doc.RootElement.TryGetProperty("as_owner", out var asOwner))
                    {
                        result = $"AS Owner: {asOwner.GetString()}";
                    }
                    if (doc.RootElement.TryGetProperty("resolutions", out var resolutions) && resolutions.GetArrayLength() > 0)
                    {
                        result += $"; {resolutions.GetArrayLength()} resolutions";
                    }
                    VirusTotalInfo = result;
                    return result;
                }
                else
                {
                    VirusTotalInfo = "No data found";
                    return "No data found";
                }
            }
            catch (Exception ex)
            {
                var error = $"VirusTotal error: {ex.Message}";
                VirusTotalInfo = error;
                return error;
            }
        }

        private async Task<string> GetLocalTimeAsync(double lat, double lon, string timezone, CancellationToken token)
        {
            string cacheKey = $"{lat:F4},{lon:F4},{timezone}";
            if (_localTimeCache.TryGetValue(cacheKey, out var cached) && DateTime.Now - cached.timestamp < CacheExpiration)
                return cached.time;

            using var localCts = CancellationTokenSource.CreateLinkedTokenSource(token);
            localCts.CancelAfter(TimeSpan.FromSeconds(3));

            var tasks = new List<Task<(bool ok, string result)>>();
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token);
                    cts.CancelAfter(TimeSpan.FromSeconds(2));
                    var url = $"https://timeapi.io/api/Time/current/coordinate?latitude={lat}&longitude={lon}";
                    var resp = await HttpClient.GetAsync(url, cts.Token);
                    if (!resp.IsSuccessStatusCode) return (false, "");
                    var json = await resp.Content.ReadAsStringAsync(cts.Token);
                    var timeData = JsonSerializer.Deserialize<TimeApiResponse>(json);
                    if (timeData != null)
                        return (true, timeData.DateTime.ToString("yyyy-MM-dd HH:mm:ss"));
                }
                catch { }
                return (false, "");
            }));

            if (!string.IsNullOrWhiteSpace(timezone))
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(localCts.Token);
                        cts.CancelAfter(TimeSpan.FromSeconds(2));
                        var url = $"https://worldtimeapi.org/api/timezone/{timezone}";
                        var resp = await HttpClient.GetAsync(url, cts.Token);
                        if (!resp.IsSuccessStatusCode) return (false, "");
                        var json = await resp.Content.ReadAsStringAsync(cts.Token);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("datetime", out var dtProp))
                        {
                            var dtStr = dtProp.GetString();
                            if (DateTime.TryParse(dtStr, out var dt))
                                return (true, dt.ToString("yyyy-MM-dd HH:mm:ss"));
                        }
                    }
                    catch { }
                    return (false, "");
                }));
            }

            try
            {
                while (tasks.Count > 0)
                {
                    var finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                    tasks.Remove(finished);
                    var (ok, result) = await finished.ConfigureAwait(false);
                    if (ok && !string.IsNullOrWhiteSpace(result))
                    {
                        _localTimeCache[cacheKey] = (result, DateTime.Now);
                        return result;
                    }
                }
            }
            catch (OperationCanceledException) { }

            var utcNow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC (local time unavailable)";
            _localTimeCache[cacheKey] = (utcNow, DateTime.Now);
            return utcNow;
        }

        private async Task SaveLookupToHistoryAsync()
        {
            if (GeolocationResult != null)
            {
                var lookup = new LookupHistory
                {
                    IpAddress = GeolocationResult.Query,
                    Country = GeolocationResult.Country,
                    CountryCode = GeolocationResult.CountryCode,
                    RegionName = GeolocationResult.RegionName,
                    City = GeolocationResult.City,
                    Latitude = GeolocationResult.Lat,
                    Longitude = GeolocationResult.Lon,
                    Timezone = GeolocationResult.Timezone,
                    Isp = GeolocationResult.Isp,
                    Query = GeolocationResult.Query,
                    ThreatScore = ThreatScore,
                    ThreatInfo = ThreatInfo,
                    LookupTime = DateTime.UtcNow,
                    LookupDuration = LookupDuration
                };
                try
                {
                    await _historyService.AddLookupAsync(lookup);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save lookup to history: {ex.Message}");
                }
            }
        }

        private void SetStatus(string message, bool isSuccess = false, bool isError = false, bool isWorking = false)
        {
            StatusMessage = message;
            if (isSuccess) StatusBrush = Brushes.LawnGreen;
            else if (isError) StatusBrush = Brushes.Red;
            else if (isWorking) StatusBrush = Brushes.DodgerBlue;
            else StatusBrush = Brushes.Gray;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Data Models
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

    public record TimeApiResponse(
        [property: JsonPropertyName("dateTime")] DateTime DateTime
    );
}