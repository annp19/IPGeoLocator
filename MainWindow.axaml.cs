using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using IPGeoLocator.Data;
using IPGeoLocator.Models;
using IPGeoLocator.Services;
using IPGeoLocator.ViewModels;
using IPGeoLocator.Views;
using IPGeoLocator.Controls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NewtonsoftJson = Newtonsoft.Json;
using IPGeoLocator.Utilities;

namespace IPGeoLocator;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Optimized HttpClient with connection pooling and better defaults
    private static readonly HttpClient HttpClient = new HttpClient(new SocketsHttpHandler
    {
        // Connection pooling settings
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 20,  // Increased for better concurrent performance
        
        // Timeout settings
        ConnectTimeout = TimeSpan.FromSeconds(5),
        
        // Automatic redirection and decompression
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        
        // Use in-memory DNS cache to avoid repeated DNS lookups
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromSeconds(8), // Global timeout for all requests
        // Add default headers to reduce request size
        DefaultRequestHeaders = 
        {
            { "User-Agent", "IPGeoLocator/1.0" },
            { "Accept", "application/json" }
        }
    };

    // Database context, history service, and performance service
    private readonly AppDbContext _dbContext;
    private readonly LookupHistoryService _historyService;
    private readonly PerformanceService _performanceService;
    private readonly NetworkToolsService _networkToolsService;

    // In-memory caches for performance with expiration and automatic cleanup
    private static readonly Dictionary<string, (GeolocationResponse? response, DateTime timestamp)> _geoCache = new();
    private static readonly Dictionary<string, (Bitmap? bitmap, DateTime timestamp)> _flagCache = new();
    // In-memory cache for local time lookups (key: lat,lon,timezone)
    private static readonly Dictionary<string, (string time, DateTime timestamp)> _localTimeCache = new();
    // Cache expiration time (15 minutes for more responsive threat intelligence)
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    // Maximum cache size to prevent memory leaks
    private static readonly int MaxCacheSize = 1000;

    // Method to clean up expired cache entries to prevent memory leaks
    private static void CleanupExpiredCacheEntries()
    {
        var now = DateTime.Now;
        
        // Clean up expired geolocation cache entries
        var expiredGeoKeys = _geoCache.Where(kvp => now - kvp.Value.timestamp > CacheExpiration)
                                      .Select(kvp => kvp.Key)
                                      .ToList();
        foreach (var key in expiredGeoKeys)
        {
            _geoCache.Remove(key);
        }
        
        // If cache is still too large, remove oldest entries
        while (_geoCache.Count > MaxCacheSize)
        {
            var oldestKey = _geoCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
            _geoCache.Remove(oldestKey);
        }
        
        // Clean up expired flag cache entries and dispose bitmaps
        var expiredFlagKeys = _flagCache.Where(kvp => now - kvp.Value.timestamp > CacheExpiration)
                                        .Select(kvp => kvp.Key)
                                        .ToList();
        foreach (var key in expiredFlagKeys)
        {
            // Dispose bitmap to prevent memory leaks
            if (_flagCache[key].bitmap != null)
            {
                try
                {
                    _flagCache[key].bitmap.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _flagCache.Remove(key);
        }
        
        // If flag cache is still too large, remove oldest entries
        while (_flagCache.Count > MaxCacheSize)
        {
            var oldestKey = _flagCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
            var bitmap = _flagCache[oldestKey].bitmap;
            if (bitmap != null)
            {
                try
                {
                    bitmap.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _flagCache.Remove(oldestKey);
        }
        
        // Clean up expired local time cache entries
        var expiredTimeKeys = _localTimeCache.Where(kvp => now - kvp.Value.timestamp > CacheExpiration)
                                             .Select(kvp => kvp.Key)
                                             .ToList();
        foreach (var key in expiredTimeKeys)
        {
            _localTimeCache.Remove(key);
        }
        
        // If time cache is still too large, remove oldest entries
        while (_localTimeCache.Count > MaxCacheSize)
        {
            var oldestKey = _localTimeCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
            _localTimeCache.Remove(oldestKey);
        }
    }
    
    // Method to dispose all cached bitmaps to prevent memory leaks
    public static void DisposeCachedResources()
    {
        // Dispose all cached bitmaps
        foreach (var kvp in _flagCache)
        {
            if (kvp.Value.bitmap != null)
            {
                try
                {
                    kvp.Value.bitmap.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
        
        // Clear all caches
        _geoCache.Clear();
        _flagCache.Clear();
        _localTimeCache.Clear();
    }

    // Threat info properties
    private string _threatInfo = "N/A";
    private int _threatScore = -1;
    
    // Additional threat intelligence properties
    private string _abuseipdbInfo = "N/A";
    private string _virustotalInfo = "N/A";
    private string _threatCrowdInfo = "N/A";

    // New feature command properties
    private bool _isScanningRange;
    private string _rangeScanStatus = "Ready";
    private int _rangeScanProgress;
    
    // UI State Properties
    private string _ipAddressInput = "";
    private string _abuseIpDbApiKey = "";
    private string _statusMessage = "Enter an IP and click Lookup.";
    private bool _isLoading;
    private bool _isResultVisible;
    private ISolidColorBrush _statusBrush = Brushes.Gray;
    private GeolocationResponse? _geolocationResult;
    private string _localTime = "N/A";
    private string _lookupDuration = "";
    private Bitmap? _flagImage;
    private bool _isDarkTheme;
    private int _selectedLanguageIndex;
    private PerformanceMetrics _performanceMetrics = new PerformanceMetrics();

    // Data-bound properties
    public string IpAddressInput { get => _ipAddressInput; set => SetProperty(ref _ipAddressInput, value); }
    public string AbuseIpDbApiKey { get => _abuseIpDbApiKey; set => SetProperty(ref _abuseIpDbApiKey, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public bool IsResultVisible { get => _isResultVisible; set => SetProperty(ref _isResultVisible, value); }
    public ISolidColorBrush StatusBrush { get => _statusBrush; set => SetProperty(ref _statusBrush, value); }
    public GeolocationResponse? GeolocationResult { get => _geolocationResult; set => SetProperty(ref _geolocationResult, value); }
    public string LocalTime { get => _localTime; set => SetProperty(ref _localTime, value); }
    public Bitmap? FlagImage { get => _flagImage; set => SetProperty(ref _flagImage, value); }
    public string LookupDuration { get => _lookupDuration; set => SetProperty(ref _lookupDuration, value); }
    public bool IsDarkTheme { get => _isDarkTheme; set { SetProperty(ref _isDarkTheme, value); UpdateTheme(); } }
    public int SelectedLanguageIndex { get => _selectedLanguageIndex; set { SetProperty(ref _selectedLanguageIndex, value); UpdateLocale(); } }
    public string ThreatInfo { get => _threatInfo; set => SetProperty(ref _threatInfo, value); }
    public int ThreatScore { get => _threatScore; set => SetProperty(ref _threatScore, value); }
    public string AbuseIpDbInfo { get => _abuseipdbInfo; set => SetProperty(ref _abuseipdbInfo, value); }
    public string VirusTotalInfo { get => _virustotalInfo; set => SetProperty(ref _virustotalInfo, value); }
    public string ThreatCrowdInfo { get => _threatCrowdInfo; set => SetProperty(ref _threatCrowdInfo, value); }
    
    // Performance metrics property
    public PerformanceMetrics PerformanceMetrics { get => _performanceMetrics; set => SetProperty(ref _performanceMetrics, value); }
    
    // New feature properties for data binding
    public bool IsScanningRange { get => _isScanningRange; set => SetProperty(ref _isScanningRange, value); }
    public string RangeScanStatus { get => _rangeScanStatus; set => SetProperty(ref _rangeScanStatus, value); }
    public int RangeScanProgress { get => _rangeScanProgress; set => SetProperty(ref _rangeScanProgress, value); }
    
    // Computed properties for display
    public string LocationString => $"{GeolocationResult?.City}, {GeolocationResult?.RegionName}, {GeolocationResult?.Country}";
    public string CoordinatesString => $"{GeolocationResult?.Lat:F4}, {GeolocationResult?.Lon:F4}";
    
    // Localization
    public Dictionary<string, string> Locale { get; private set; } = new();
    private readonly Dictionary<string, Dictionary<string, string>> _locales = new()
    {
        { "en", new Dictionary<string, string> {
            { "Title", "IP Geolocation Tool" },
            { "IPAddressLabel", "IP Address" },
            { "AbuseIPDBKeyLabel", "AbuseIPDB API Key" },
            { "OptionalLabel", "Optional - for threat intelligence" },
            { "GetMyIPButton", "Get My IP" },
            { "LookupButton", "Lookup" },
            { "ThemeLabel", "Dark Mode" },
            { "ResultsTitle", "Geolocation Results" },
            { "IPAddressResultLabel", "IP Address:" },
            { "LocationLabel", "Location:" },
            { "ISPLabel", "ISP / Org:" },
            { "CoordinatesLabel", "Coordinates:" },
            { "TimezoneLabel", "Timezone:" },
            { "LocalTimeLabel", "Current Local Time:" },
            { "ThreatIntelLabel", "Threat Intelligence:" },
            { "CopyAllButton", "Copy All Info" },
            { "CopyCoordsButton", "Copy Coordinates" },
            { "StatusReady", "Enter an IP and click Lookup." },
            { "StatusInvalidIP", "Invalid IP address format." },
            { "StatusFetching", "Fetching data..." },
            { "StatusSuccess", "Lookup successful!" },
            { "StatusError", "An error occurred: " },
            { "StatusCopied", "Copied to clipboard!" },
            { "ThreatClean", "Clean (Score < 30)" },
            { "ThreatSuspicious", "Suspicious (Score 30-79)" },
            { "ThreatDangerous", "Dangerous/Blacklisted (Score >= 80)" },
            { "ThreatNoKey", "API key not provided." },
        }},
        { "vi", new Dictionary<string, string> {
            { "Title", "Công cụ Định vị IP" },
            { "IPAddressLabel", "Địa chỉ IP" },
            { "AbuseIPDBKeyLabel", "Khóa API AbuseIPDB" },
            { "OptionalLabel", "Không bắt buộc - để phân tích mối đe dọa" },
            { "GetMyIPButton", "Lấy IP của tôi" },
            { "LookupButton", "Tra cứu" },
            { "ThemeLabel", "Chế độ tối" },
            { "ResultsTitle", "Kết quả định vị" },
            { "IPAddressResultLabel", "Địa chỉ IP:" },
            { "LocationLabel", "Vị trí:" },
            { "ISPLabel", "ISP / Tổ chức:" },
            { "CoordinatesLabel", "Tọa độ:" },
            { "TimezoneLabel", "Múi giờ:" },
            { "LocalTimeLabel", "Giờ địa phương:" },
            { "ThreatIntelLabel", "Phân tích mối đe dọa:" },
            { "CopyAllButton", "Sao chép tất cả" },
            { "CopyCoordsButton", "Sao chép tọa độ" },
            { "StatusReady", "Nhập IP và nhấn Tra cứu." },
            { "StatusInvalidIP", "Định dạng địa chỉ IP không hợp lệ." },
            { "StatusFetching", "Đang lấy dữ liệu..." },
            { "StatusSuccess", "Tra cứu thành công!" },
            { "StatusError", "Đã xảy ra lỗi: " },
            { "StatusCopied", "Đã sao chép vào clipboard!" },
            { "ThreatClean", "An toàn (Điểm < 30)" },
            { "ThreatSuspicious", "Nghi ngờ (Điểm 30-79)" },
            { "ThreatDangerous", "Nguy hiểm/Danh sách đen (Điểm >= 80)" },
            { "ThreatNoKey", "Chưa cung cấp khóa API." },
        }}
    };

    public MainWindow()
    {
        _dbContext = new AppDbContext();
        _historyService = new LookupHistoryService(_dbContext);
        _performanceService = new PerformanceService();
        _networkToolsService = new NetworkToolsService();
        
        InitializeComponent();
        DataContext = this;
        UpdateLocale(); // Set default language
        
        // Initialize threat visualization after the control is loaded
        this.AttachedToVisualTree += (sender, e) =>
        {
            InitializeThreatVisualization();
        };
    }
    
    // Commands for buttons
    public async Task GetMyIpCommand()
    {
    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
        try
        {
            IpAddressInput = await HttpClient.GetStringAsync("https://api.ipify.org", cts.Token).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            SetStatus(Locale["StatusError"] + "Timeout getting IP.", isError: true);
        }
        catch (Exception ex)
        {
            SetStatus(Locale["StatusError"] + ex.Message, isError: true);
        }
    }

public async Task LookupIpCommand()
{
    var validationResult = Utilities.IpValidator.ValidateIpInput(IpAddressInput);
    if (!validationResult.IsValid)
    {
        SetStatus(validationResult.Error ?? Locale["StatusInvalidIP"], isError: true);
        return;
    }
    
    // Check if it's a single IP (the only type we currently support in main lookup)
    if (validationResult.Type != IpInputType.SingleIp)
    {
        SetStatus("This lookup type is not supported in main lookup. Use the IP Range Scanner for ranges or CIDR blocks.", isError: true);
        return;
    }

    IsLoading = true;
    IsResultVisible = false;
    SetStatus(Locale["StatusFetching"], isWorking: true);

    _performanceService.StartOperation("IP_Lookup");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
    try
    {
        // Start geolocation task with priority
        _performanceService.StartOperation("Geolocation");
        var geoTask = GetGeolocationAsync(IpAddressInput, cts.Token);
        var geoResult = await geoTask;
        _performanceService.EndOperation("Geolocation");
        GeolocationResult = geoResult;

        if (GeolocationResult?.Status != "success")
        {
            SetStatus(Locale["StatusError"] + (GeolocationResult?.Message ?? "Unknown geolocation error"), isError: true);
            IsLoading = false;
            LookupDuration = "";
            _performanceService.EndOperation("IP_Lookup");
            return;
        }

        // Start all dependent tasks concurrently with optimized timeouts
        _performanceService.StartOperation("LocalTime");
        var timeTask = GetLocalTimeAsync(GeolocationResult.Lat, GeolocationResult.Lon, GeolocationResult.Timezone, cts.Token);
        
        var flagTask = GetCountryFlagAsync(GeolocationResult.CountryCode, cts.Token);
        
        _performanceService.StartOperation("ThreatCheck");
        var threatTask = GetThreatInfoAsync(GeolocationResult.Query, AbuseIpDbApiKey, cts.Token);

        // Create a limited time window for secondary tasks (3 seconds max)
        using var secondaryCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        secondaryCts.CancelAfter(TimeSpan.FromSeconds(3));

        try
        {
            // Wait for all secondary tasks with the limited time window
            await Task.WhenAll(
                timeTask.ContinueWith(t => { }, secondaryCts.Token), // Don't let exceptions propagate
                flagTask.ContinueWith(t => { }, secondaryCts.Token),  // Don't let exceptions propagate
                threatTask.ContinueWith(t => { }, secondaryCts.Token)  // Don't let exceptions propagate
            );
        }
        catch (OperationCanceledException)
        {
            // Secondary tasks timed out, but we can continue with primary data
            System.Diagnostics.Debug.WriteLine("Secondary tasks timed out, continuing with primary data");
        }
        finally
        {
            // Ensure operations are ended even if they were cancelled
            _performanceService.EndOperation("LocalTime");
            _performanceService.EndOperation("ThreatCheck");
        }

        // Set results that completed (failed/cancelled tasks will be handled gracefully)
        try
        {
            LocalTime = timeTask.IsCompletedSuccessfully ? timeTask.Result : "Unavailable";
        }
        catch { LocalTime = "Unavailable"; }
        
        try
        {
            FlagImage = flagTask.IsCompletedSuccessfully ? flagTask.Result : null;
        }
        catch { FlagImage = null; }
        
        try
        {
            if (threatTask.IsCompletedSuccessfully)
            {
                ThreatInfo = threatTask.Result;
                // Extract score if possible
                ThreatScore = ParseThreatScore(ThreatInfo);
            }
        }
        catch { ThreatInfo = "Unavailable"; }

        sw.Stop();
        LookupDuration = $"Lookup time: {sw.ElapsedMilliseconds} ms";
        _performanceService.RecordMetric("LookupDurationMs", sw.ElapsedMilliseconds);

        // Update performance metrics display
        PerformanceMetrics = _performanceService.GetCurrentMetrics();

        IsResultVisible = true;
        SetStatus(Locale["StatusSuccess"], isSuccess: true);

        // Save to history in background (don't block UI)
        _ = Task.Run(async () => 
        {
            try 
            { 
                await SaveLookupToHistoryAsync(); 
            } 
            catch (Exception ex) 
            { 
                System.Diagnostics.Debug.WriteLine($"Failed to save to history: {ex.Message}"); 
            } 
        });
    }
    catch (TaskCanceledException)
    {
        IsResultVisible = false;
        SetStatus(Locale["StatusError"] + "Lookup timed out.", isError: true);
        LookupDuration = "";
    }
    catch (Exception ex)
    {
        IsResultVisible = false;
        SetStatus(Locale["StatusError"] + ex.Message, isError: true);
        LookupDuration = "";
    }
    finally
    {
        _performanceService.EndOperation("IP_Lookup");
        IsLoading = false;
        
        // Update threat visualization
        UpdateThreatVisualization();
    }
}
    
    public async Task CopyAllCommand()
    {
        if (GeolocationResult == null) return;
        var sb = new StringBuilder();
        sb.AppendLine($"{Locale["IPAddressResultLabel"]} {GeolocationResult.Query}");
        sb.AppendLine($"{Locale["LocationLabel"]} {LocationString}");
        sb.AppendLine($"{Locale["ISPLabel"]} {GeolocationResult.Isp}");
        sb.AppendLine($"{Locale["CoordinatesLabel"]} {CoordinatesString}");
        sb.AppendLine($"{Locale["TimezoneLabel"]} {GeolocationResult.Timezone}");
        sb.AppendLine($"{Locale["LocalTimeLabel"]} {LocalTime}");
        await (TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(sb.ToString()) ?? Task.CompletedTask);
        SetStatus(Locale["StatusCopied"], isSuccess: true);
    }
    
    public async Task CopyCoordsCommand()
    {
        if (GeolocationResult == null) return;
        await (TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(CoordinatesString) ?? Task.CompletedTask);
        SetStatus(Locale["StatusCopied"], isSuccess: true);
    }
    
    public async Task ShowHistoryCommand()
    {
        var historyWindow = new HistoryWindow(_historyService);
        historyWindow.SelectedIpCallback = (ip) =>
        {
            // When an IP is selected in the history window, populate it in the main input
            IpAddressInput = ip;
        };
        await historyWindow.ShowDialog(this); // Show as modal dialog
    }
    
    public async Task ExportResultsCommand()
    {
        if (GeolocationResult == null)
        {
            SetStatus("No results to export.", isError: true);
            return;
        }

        try
        {
            // Show a dialog to let user choose the export format
            var dialog = new OpenFileDialog()
            {
                Title = "Export Results",
                Filters = new System.Collections.Generic.List<FileDialogFilter>
                {
                    new FileDialogFilter { Name = "JSON Files", Extensions = new System.Collections.Generic.List<string> { "json" } },
                    new FileDialogFilter { Name = "CSV Files", Extensions = new System.Collections.Generic.List<string> { "csv" } },
                    new FileDialogFilter { Name = "Text Files", Extensions = new System.Collections.Generic.List<string> { "txt" } }
                }
            };

            var result = await dialog.ShowAsync(this);
            if (result != null && result.Length > 0)
            {
                string filePath = result[0];
                string extension = System.IO.Path.GetExtension(filePath).ToLower();

                switch (extension)
                {
                    case ".json":
                        await ExportToJson(filePath);
                        break;
                    case ".csv":
                        await ExportToCsv(filePath);
                        break;
                    case ".txt":
                        await ExportToTxt(filePath);
                        break;
                    default:
                        SetStatus($"Unsupported file format: {extension}", isError: true);
                        return;
                }

                SetStatus($"Results exported to: {filePath}", isSuccess: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    private async Task ExportToJson(string filePath)
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

        string json = NewtonsoftJson.JsonConvert.SerializeObject(exportData, NewtonsoftJson.Formatting.Indented);
        await System.IO.File.WriteAllTextAsync(filePath, json);
    }

    private async Task ExportToCsv(string filePath)
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

        await System.IO.File.WriteAllTextAsync(filePath, csv.ToString());
    }

    private async Task ExportToTxt(string filePath)
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

        await System.IO.File.WriteAllTextAsync(filePath, txt.ToString());
    }

    // API Integration Methods
    private async Task<GeolocationResponse?> GetGeolocationAsync(string ip, System.Threading.CancellationToken token)
    {
        // Check cache with expiration
        if (_geoCache.TryGetValue(ip, out var cached) && DateTime.Now - cached.timestamp < CacheExpiration)
            return cached.response;

        // Clean up expired cache entries to prevent memory leaks
        CleanupExpiredCacheEntries();

        try
        {
            var response = await HttpClient.GetAsync($"http://ip-api.com/json/{ip}", token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<GeolocationResponse>(json);
            
            // Limit cache size to prevent memory leaks
            if (_geoCache.Count >= MaxCacheSize)
            {
                // Remove oldest entry
                var oldestKey = _geoCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
                _geoCache.Remove(oldestKey);
            }
            
            _geoCache[ip] = (result, DateTime.Now);
            return result;
        }
        catch (Exception ex)
        {
            // Log error but don't fail the entire operation
            System.Diagnostics.Debug.WriteLine($"Geolocation lookup failed: {ex.Message}");
            return null;
        }
    }
    
    private async Task<Bitmap?> GetCountryFlagAsync(string countryCode, System.Threading.CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return null;
        
        // Check cache with expiration
        if (_flagCache.TryGetValue(countryCode, out var cached) && 
            DateTime.Now - cached.timestamp < CacheExpiration && 
            cached.bitmap != null)
            return cached.bitmap;
            
        // Clean up expired cache entries to prevent memory leaks
        CleanupExpiredCacheEntries();

        try
        {
            var data = await HttpClient.GetByteArrayAsync($"https://flagcdn.com/32x24/{countryCode.ToLower()}.png", token).ConfigureAwait(false);
            var bmp = new Bitmap(new MemoryStream(data));
            
            // Limit cache size to prevent memory leaks
            if (_flagCache.Count >= MaxCacheSize)
            {
                // Remove oldest entry
                var oldestKey = _flagCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
                _flagCache.Remove(oldestKey);
            }
            
            _flagCache[countryCode] = (bmp, DateTime.Now);
            return bmp;
        }
        catch 
        { 
            // Return null on failure - graceful fallback
            _flagCache[countryCode] = (null, DateTime.Now);
            return null; 
        }
    }
    
    // Helper to extract threat score from string (if present)
    private int ParseThreatScore(string threatInfo)
    {
        if (string.IsNullOrWhiteSpace(threatInfo)) return -1;
        var digits = System.Text.RegularExpressions.Regex.Match(threatInfo, @"\d+");
        if (digits.Success && int.TryParse(digits.Value, out int score))
            return score;
        return -1;
    }

    // Threat Intelligence methods
    
    // Enhanced threat intelligence with multiple services and timeout control
    private async Task<string> GetThreatInfoAsync(string ip, string apiKey, System.Threading.CancellationToken token)
    {
        var threatTasks = new List<Task<string>>();
        
        // AbuseIPDB check if API key provided
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            threatTasks.Add(GetAbuseIpDbInfoAsync(ip, apiKey, token));
        }
        else
        {
            AbuseIpDbInfo = Locale.ContainsKey("ThreatNoKey") ? Locale["ThreatNoKey"] : "API key not provided.";
        }
        
        // VirusTotal check (requires API key)
        if (!string.IsNullOrWhiteSpace(VirusTotalApiKey))
        {
            threatTasks.Add(GetVirusTotalInfoAsync(ip, VirusTotalApiKey, token));
        }
        else
        {
            VirusTotalInfo = "API key not provided.";
        }
        
        // Other threat services can be added here
        ThreatCrowdInfo = "N/A - Service not implemented";
        
        // Run threat tasks with a reasonable timeout
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        cts.CancelAfter(TimeSpan.FromSeconds(4)); // Shorter timeout for better UX
        
        try
        {
            // Wait for threat intelligence tasks with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(3), cts.Token);
            var allTasks = Task.WhenAll(threatTasks);
            var completedTask = await Task.WhenAny(allTasks, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                // Timeout - return partial results or default
                var completedResults = threatTasks
                    .Where(t => t.IsCompletedSuccessfully)
                    .Select(t => t.Result)
                    .Where(r => !string.IsNullOrEmpty(r) && r != "N/A" && r != "API key not provided.")
                    .ToList();
                    
                return completedResults.Any() ? string.Join("; ", completedResults) : "Threat check timed out.";
            }
            
            // All tasks completed successfully
            var results = await allTasks;
            var allResults = results
                .Where(r => !string.IsNullOrEmpty(r) && r != "N/A" && r != "API key not provided.")
                .ToList();
                
            return allResults.Any() ? string.Join("; ", allResults) : "No threat data found.";
        }
        catch (OperationCanceledException)
        {
            // Handle cancellation
            return "Threat check cancelled.";
        }
        catch (Exception ex)
        {
            // Log the error but don't fail the entire operation
            System.Diagnostics.Debug.WriteLine($"Threat intelligence error: {ex.Message}");
            return "Error retrieving threat data.";
        }
    }
    
    // Command to update performance metrics
    public void UpdatePerformanceMetricsCommand()
    {
        PerformanceMetrics = _performanceService.GetCurrentMetrics();
    }
    
    // Command to reset performance metrics
    public void ResetPerformanceMetricsCommand()
    {
        _performanceService.ResetMetrics();
        PerformanceMetrics = new PerformanceMetrics();
    }
    
    // AbuseIPDB threat info
    private async Task<string> GetAbuseIpDbInfoAsync(string ip, string apiKey, System.Threading.CancellationToken token)
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
            var data = System.Text.Json.JsonSerializer.Deserialize<AbuseIpDbResponse>(json);
            if (data?.Data == null)
            {
                AbuseIpDbInfo = "No threat data.";
                return "No threat data.";
            }
            int score = data.Data.AbuseConfidenceScore;
            string scoreText;
            if (score < 30) scoreText = Locale.ContainsKey("ThreatClean") ? $"{Locale["ThreatClean"]} (Score {score})" : $"Clean (Score {score})";
            else if (score < 80) scoreText = Locale.ContainsKey("ThreatSuspicious") ? $"{Locale["ThreatSuspicious"]} (Score {score})" : $"Suspicious (Score {score})";
            else scoreText = Locale.ContainsKey("ThreatDangerous") ? $"{Locale["ThreatDangerous"]} (Score {score})" : $"Dangerous (Score {score})";
            
            AbuseIpDbInfo = scoreText;
            return scoreText;
        }
        catch (TaskCanceledException ex)
        {
            var error = $"AbuseIPDB timeout: {ex.Message}";
            AbuseIpDbInfo = error;
            return error;
        }
        catch (Exception ex)
        {
            var error = $"AbuseIPDB error: {ex.Message}";
            AbuseIpDbInfo = error;
            return error;
        }
    }
    
    // VirusTotal threat info
    private async Task<string> GetVirusTotalInfoAsync(string ip, string apiKey, System.Threading.CancellationToken token)
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
            // Parse VirusTotal response (simplified implementation)
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("response_code", out var responseCode) && responseCode.GetInt32() == 1)
            {
                // Extract relevant threat info
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
        catch (TaskCanceledException ex)
        {
            var error = $"VirusTotal timeout: {ex.Message}";
            VirusTotalInfo = error;
            return error;
        }
        catch (Exception ex)
        {
            var error = $"VirusTotal error: {ex.Message}";
            VirusTotalInfo = error;
            return error;
        }
    }
    
    // Property to hold VirusTotal API key (similar to AbuseIPDB)
    private string _virustotalApiKey = "";
    public string VirusTotalApiKey { get => _virustotalApiKey; set => SetProperty(ref _virustotalApiKey, value); }
        // Model for AbuseIPDB response
    public class AbuseIpDbResponse
    {
        [JsonPropertyName("data")]
        public AbuseIpDbData? Data { get; set; }
    }
    public class AbuseIpDbData
    {
        [System.Text.Json.Serialization.JsonPropertyName("abuseConfidenceScore")]
        public int AbuseConfidenceScore { get; set; }
    }

    private async Task<string> GetLocalTimeAsync(double lat, double lon, string timezone, System.Threading.CancellationToken token)
    {
        // Use a cache key based on coordinates and timezone
        string cacheKey = $"{lat:F4},{lon:F4},{timezone}";
        
        // Check cache with expiration
        if (_localTimeCache.TryGetValue(cacheKey, out var cached) && DateTime.Now - cached.timestamp < CacheExpiration)
            return cached.time;

        // Clean up expired cache entries to prevent memory leaks
        CleanupExpiredCacheEntries();

        // Create cancellation token with shorter timeout for faster response
        using var localCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        localCts.CancelAfter(TimeSpan.FromSeconds(3)); // Shorter timeout for better UX

        // Prepare all possible requests with timeouts
        var tasks = new List<Task<(bool ok, string result)>>();

        // timeapi.io by coordinate (fastest option)
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

        // worldtimeapi.org by timezone (good fallback)
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

        // Run all tasks concurrently and take the first successful result
        try
        {
            while (tasks.Count > 0)
            {
                var finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);
                var (ok, result) = await finished.ConfigureAwait(false);
                if (ok && !string.IsNullOrWhiteSpace(result))
                {
                    // Limit cache size to prevent memory leaks
                    if (_localTimeCache.Count >= MaxCacheSize)
                    {
                        // Remove oldest entry
                        var oldestKey = _localTimeCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
                        _localTimeCache.Remove(oldestKey);
                    }
                    
                    _localTimeCache[cacheKey] = (result, DateTime.Now);
                    return result;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout - return fallback
        }
        catch { }

        // Last fallback: system UTC time
        var utcNow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC (local time unavailable)";
        
        // Limit cache size to prevent memory leaks
        if (_localTimeCache.Count >= MaxCacheSize)
        {
            // Remove oldest entry
            var oldestKey = _localTimeCache.OrderBy(kvp => kvp.Value.timestamp).First().Key;
            _localTimeCache.Remove(oldestKey);
        }
        
        _localTimeCache[cacheKey] = (utcNow, DateTime.Now);
        return utcNow;
    }

    // Save lookup to history database
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
                // Log the error but don't show to user since it's not critical for the lookup
                System.Diagnostics.Debug.WriteLine($"Failed to save lookup to history: {ex.Message}");
            }
        }
    }

    // UI and State Helpers
    private void SetStatus(string message, bool isSuccess = false, bool isError = false, bool isWorking = false)
    {
        StatusMessage = message;
        if (isSuccess) StatusBrush = (ISolidColorBrush)this.FindResource("SuccessBrush")!;
        else if (isError) StatusBrush = (ISolidColorBrush)this.FindResource("DangerBrush")!;
        else if (isWorking) StatusBrush = (ISolidColorBrush)this.FindResource("PrimaryBrush")!;
        else StatusBrush = Brushes.Gray;
    }
    
    private void UpdateTheme()
    {
        if (Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = IsDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
    
    private void UpdateLocale()
    {
        var langCode = SelectedLanguageIndex == 1 ? "vi" : "en";
        Locale = _locales[langCode];
        
        // Notify UI of all localizable properties changing
        OnPropertyChanged(nameof(Locale));
        // Reset status message with new language
        SetStatus(Locale["StatusReady"]); 
    }

    // INotifyPropertyChanged Implementation
    public new event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    // Cache warming method - pre-fetches common lookups to improve performance
    private async Task WarmCacheAsync()
    {
        // Pre-populate cache with common IP addresses for faster subsequent lookups
        var commonIps = new[] { "8.8.8.8", "1.1.1.1", "8.8.4.4", "1.0.0.1" };
        
        foreach (var ip in commonIps)
        {
            try
            {
                // Don't wait for these operations, just fire and forget to warm the cache
                _ = Task.Run(async () => await GetGeolocationAsync(ip, CancellationToken.None));
            }
            catch
            {
                // Ignore errors during cache warming
            }
        }
    }
    
    protected override void OnOpened(EventArgs e)
    {
        // Warm the cache when the application starts
        _ = Task.Run(async () => await WarmCacheAsync());
        base.OnOpened(e);
    }
    
    protected override void OnClosed(EventArgs e)
    {
        _dbContext?.Dispose();
        DisposeCachedResources(); // Clean up cached resources to prevent memory leaks
        base.OnClosed(e);
    }

/// <summary>
/// Method to scan a single IP address and return the result
/// </summary>
public async Task<IpScanResult> ScanIpAddressAsync(string ipAddress)
{
    try
    {
        var result = await GetGeolocationAsync(ipAddress, CancellationToken.None);
        if (result != null && result.Status == "success")
        {
            // Get threat info
            var threatInfo = await GetThreatInfoAsync(result.Query, AbuseIpDbApiKey, CancellationToken.None);
            var threatScore = ParseThreatScore(threatInfo);
            
            // Get local time
            var localTime = await GetLocalTimeAsync(result.Lat, result.Lon, result.Timezone, new CancellationToken());
            
            return new IpScanResult
            {
                IpAddress = result.Query,
                Status = "Success",
                Country = result.Country,
                City = result.City,
                Isp = result.Isp,
                ThreatScore = threatScore,
                ErrorMessage = ""
            };
        }
        else
        {
            return new IpScanResult
            {
                IpAddress = ipAddress,
                Status = "Failed",
                ErrorMessage = result?.Message ?? "Geolocation failed"
            };
        }
    }
    catch (Exception ex)
    {
        return new IpScanResult
        {
            IpAddress = ipAddress,
            Status = "Error",
            ErrorMessage = ex.Message
        };
    }
}

// New feature command methods for IP range scanning and world map visualization

/// <summary>
/// Command to start scanning a range of IP addresses
/// </summary>
public async Task StartIpRangeScanCommand()
{
    // Open the IP range scanner window
    var rangeScannerWindow = new IpRangeScannerWindow();
    
    // Pass the API key to the scanner window
    rangeScannerWindow.AbuseIpDbApiKey = this.AbuseIpDbApiKey;
    rangeScannerWindow.VirusTotalApiKey = this.VirusTotalApiKey;
    
    await rangeScannerWindow.ShowDialog(this);
}

/// <summary>
/// Command to show the world map visualization
/// </summary>
public async Task ShowWorldMapCommand()
{
    try
    {
        // Open the world map window
        var worldMapWindow = new WorldMapWindow();
        await worldMapWindow.ShowDialog(this);
    }
    catch (Exception ex)
    {
        SetStatus($"Failed to open world map: {ex.Message}", isError: true);
    }
}


/// <summary>
/// Command to export results to the world map format
/// </summary>
public async Task ExportToWorldMapCommand()
{
    if (GeolocationResult == null)
    {
        SetStatus("No results to export to world map", isError: true);
        return;
    }

    try
    {
        // Create a location from the current geolocation result
        var location = new IpLocation
        {
            IpAddress = GeolocationResult.Query,
            Latitude = GeolocationResult.Lat,
            Longitude = GeolocationResult.Lon,
            Country = GeolocationResult.Country,
            City = GeolocationResult.City,
            Isp = GeolocationResult.Isp,
            ThreatScore = ThreatScore
        };
        
        // Open the world map window and add this location
        var worldMapWindow = new WorldMapWindow();
        worldMapWindow.AddIpLocation(location);
        await worldMapWindow.ShowDialog(this);
        
        SetStatus("Results exported to world map", isSuccess: true);
    }
    catch (Exception ex)
    {
        SetStatus($"Failed to export to world map: {ex.Message}", isError: true);
    }
}

/// <summary>
/// Command to test connectivity to the target IP using ping
/// </summary>
public async Task PingCommand()
{
    if (string.IsNullOrWhiteSpace(IpAddressInput))
    {
        SetStatus("Please enter an IP address to ping.", isError: true);
        return;
    }

    SetStatus("Pinging...", isWorking: true);
    IsLoading = true;

    try
    {
        var result = await _networkToolsService.PingAsync(IpAddressInput);
        
        if (result.Success)
        {
            SetStatus("Ping successful!", isSuccess: true);
            // In a real implementation, you might want to show the ping results in a separate UI element
            System.Diagnostics.Debug.WriteLine($"Ping output: {result.Output}");
        }
        else
        {
            SetStatus($"Ping failed: {result.Output}", isError: true);
        }
    }
    catch (Exception ex)
    {
        SetStatus($"Ping error: {ex.Message}", isError: true);
    }
    finally
    {
        IsLoading = false;
    }
}

/// <summary>
/// Command to trace the route to the target IP
/// </summary>
public async Task TracerouteCommand()
{
    if (string.IsNullOrWhiteSpace(IpAddressInput))
    {
        SetStatus("Please enter an IP address to trace.", isError: true);
        return;
    }

    SetStatus("Tracing route...", isWorking: true);
    IsLoading = true;

    try
    {
        var result = await _networkToolsService.TracerouteAsync(IpAddressInput);
        
        if (result.Success)
        {
            SetStatus("Traceroute completed!", isSuccess: true);
            // In a real implementation, you might want to show the traceroute results in a separate UI element
            System.Diagnostics.Debug.WriteLine($"Traceroute output: {result.Output}");
        }
        else
        {
            SetStatus($"Traceroute failed: {result.Output}", isError: true);
        }
    }
    catch (Exception ex)
    {
        SetStatus($"Traceroute error: {ex.Message}", isError: true);
    }
    finally
    {
        IsLoading = false;
    }
}

/// <summary>
/// Command to get WHOIS information for the target IP
/// </summary>
public async Task WhoisCommand()
{
    if (string.IsNullOrWhiteSpace(IpAddressInput))
    {
        SetStatus("Please enter an IP address for WHOIS lookup.", isError: true);
        return;
    }

    SetStatus("Getting WHOIS information...", isWorking: true);
    IsLoading = true;

    try
    {
        var result = await _networkToolsService.WhoisAsync(IpAddressInput);
        
        if (result.Success)
        {
            SetStatus("WHOIS lookup completed!", isSuccess: true);
            // In a real implementation, you might want to show the WHOIS results in a separate UI element
            System.Diagnostics.Debug.WriteLine($"WHOIS output: {result.Output}");
        }
        else
        {
            SetStatus($"WHOIS lookup failed: {result.Output}", isError: true);
        }
    }
    catch (Exception ex)
    {
        SetStatus($"WHOIS error: {ex.Message}", isError: true);
    }
    finally
    {
        IsLoading = false;
    }
}

// Data Models for JSON Deserialization
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

// RelayCommand implementation
public class RelayCommand : System.Windows.Input.ICommand
{
    private readonly Action _execute;
    private readonly Func<bool> _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute ?? (() => true);
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute();
    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

// Performance commands
private System.Windows.Input.ICommand? _updatePerformanceCommand;
private System.Windows.Input.ICommand? _resetPerformanceCommand;

public System.Windows.Input.ICommand UpdatePerformanceCommand => 
    _updatePerformanceCommand ??= new RelayCommand(UpdatePerformanceMetricsCommand);

public System.Windows.Input.ICommand ResetPerformanceCommand => 
    _resetPerformanceCommand ??= new RelayCommand(ResetPerformanceMetricsCommand);

// Threat Visualization methods
private ThreatVisualizationControl? _threatVisualizationControl;

private void InitializeThreatVisualization()
{
    _threatVisualizationControl = this.FindControl<ThreatVisualizationControl>("ThreatVisualization");
    UpdateThreatVisualization();
}

private void UpdateThreatVisualization()
{
    if (_threatVisualizationControl == null) return;
    
    // Create threat data points based on the threat information we have
    var threatData = new List<ThreatDataPoint>();
    
    if (ThreatScore >= 0)
    {
        threatData.Add(new ThreatDataPoint 
        { 
            ServiceName = "Overall", 
            ThreatScore = ThreatScore 
        });
    }
    
    if (!string.IsNullOrEmpty(AbuseIpDbInfo) && AbuseIpDbInfo != "N/A" && ThreatScore >= 0)
    {
        threatData.Add(new ThreatDataPoint 
        { 
            ServiceName = "AbuseIPDB", 
            ThreatScore = ThreatScore // This would be extracted from the specific service response
        });
    }
    
    if (!string.IsNullOrEmpty(VirusTotalInfo) && VirusTotalInfo != "N/A")
    {
        // For now, we'll use a placeholder - in a real implementation, 
        // we would parse the actual VirusTotal score
        threatData.Add(new ThreatDataPoint 
        { 
            ServiceName = "VirusTotal", 
            ThreatScore = Math.Min(100, ThreatScore + 10) // Placeholder calculation
        });
    }
    
    _threatVisualizationControl.UpdateThreatVisualization(threatData);
}

/// <summary>
/// Command to test connectivity to the target IP using ping
/// </summary>

/// <summary>
/// Command to trace the route to the target IP
/// </summary>

/// <summary>
/// Command to get WHOIS information for the target IP
/// </summary>
}
