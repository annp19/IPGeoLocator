using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using IPGeoLocator.Models;
using IPGeoLocator.Services;

namespace IPGeoLocator.ViewModels;

/// <summary>
/// ViewModel for MainWindow following MVVM pattern
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly GeolocationService _geolocationService;
    private readonly LookupHistoryService _historyService;
    private readonly PerformanceService _performanceService;
    private readonly ThreatIntelligenceService _threatService;

    // Private fields
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
    private bool _isDarkTheme;
    private int _selectedLanguageIndex;
    private string _threatInfo = "N/A";
    private int _threatScore = -1;
    private string _abuseipdbInfo = "N/A";
    private string _virustotalInfo = "N/A";
    private PerformanceMetrics _performanceMetrics = new();

    public MainWindowViewModel(
        GeolocationService geolocationService,
        LookupHistoryService historyService,
        PerformanceService performanceService,
        ThreatIntelligenceService threatService)
    {
        _geolocationService = geolocationService ?? throw new ArgumentNullException(nameof(geolocationService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _performanceService = performanceService ?? throw new ArgumentNullException(nameof(performanceService));
        _threatService = threatService ?? throw new ArgumentNullException(nameof(threatService));
        
        UpdateLocale();
    }

    // Public Properties
    public string IpAddressInput
    {
        get => _ipAddressInput;
        set => SetProperty(ref _ipAddressInput, value);
    }

    public string AbuseIpDbApiKey
    {
        get => _abuseIpDbApiKey;
        set => SetProperty(ref _abuseIpDbApiKey, value);
    }

    public string VirusTotalApiKey
    {
        get => _virusTotalApiKey;
        set => SetProperty(ref _virusTotalApiKey, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsResultVisible
    {
        get => _isResultVisible;
        set => SetProperty(ref _isResultVisible, value);
    }

    public ISolidColorBrush StatusBrush
    {
        get => _statusBrush;
        set => SetProperty(ref _statusBrush, value);
    }

    public GeolocationResponse? GeolocationResult
    {
        get => _geolocationResult;
        set
        {
            if (SetProperty(ref _geolocationResult, value))
            {
                OnPropertyChanged(nameof(LocationString));
                OnPropertyChanged(nameof(CoordinatesString));
            }
        }
    }

    public string LocalTime
    {
        get => _localTime;
        set => SetProperty(ref _localTime, value);
    }

    public string LookupDuration
    {
        get => _lookupDuration;
        set => SetProperty(ref _lookupDuration, value);
    }

    public Bitmap? FlagImage
    {
        get => _flagImage;
        set => SetProperty(ref _flagImage, value);
    }

    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => SetProperty(ref _isDarkTheme, value);
    }

    public int SelectedLanguageIndex
    {
        get => _selectedLanguageIndex;
        set
        {
            if (SetProperty(ref _selectedLanguageIndex, value))
            {
                UpdateLocale();
            }
        }
    }

    public string ThreatInfo
    {
        get => _threatInfo;
        set => SetProperty(ref _threatInfo, value);
    }

    public int ThreatScore
    {
        get => _threatScore;
        set => SetProperty(ref _threatScore, value);
    }

    public string AbuseIpDbInfo
    {
        get => _abuseipdbInfo;
        set => SetProperty(ref _abuseipdbInfo, value);
    }

    public string VirusTotalInfo
    {
        get => _virustotalInfo;
        set => SetProperty(ref _virustotalInfo, value);
    }

    public PerformanceMetrics PerformanceMetrics
    {
        get => _performanceMetrics;
        set => SetProperty(ref _performanceMetrics, value);
    }

    // Computed properties
    public string LocationString => GeolocationResult != null 
        ? $"{GeolocationResult.City}, {GeolocationResult.RegionName}, {GeolocationResult.Country}"
        : "N/A";

    public string CoordinatesString => GeolocationResult != null
        ? $"{GeolocationResult.Lat:F4}, {GeolocationResult.Lon:F4}"
        : "N/A";

    // Localization
    public Dictionary<string, string> Locale { get; private set; } = new();

    private readonly Dictionary<string, Dictionary<string, string>> _locales = new()
    {
        { "en", new Dictionary<string, string> {
            { "Title", "IP Geolocation Tool" },
            { "IPAddressLabel", "IP Address" },
            { "LookupButton", "Lookup" },
            { "StatusReady", "Enter an IP and click Lookup." },
            { "StatusInvalidIP", "Invalid IP address format." },
            { "StatusFetching", "Fetching data..." },
            { "StatusSuccess", "Lookup successful!" },
            { "StatusError", "An error occurred: " },
            { "StatusCopied", "Copied to clipboard!" },
        }},
        { "vi", new Dictionary<string, string> {
            { "Title", "Công cụ Định vị IP" },
            { "IPAddressLabel", "Địa chỉ IP" },
            { "LookupButton", "Tra cứu" },
            { "StatusReady", "Nhập IP và nhấn Tra cứu." },
            { "StatusInvalidIP", "Định dạng địa chỉ IP không hợp lệ." },
            { "StatusFetching", "Đang lấy dữ liệu..." },
            { "StatusSuccess", "Tra cứu thành công!" },
            { "StatusError", "Đã xảy ra lỗi: " },
            { "StatusCopied", "Đã sao chép vào clipboard!" },
        }}
    };

    // Commands

    public async Task GetMyIpAsync()
    {
        try
        {
            var ip = await _geolocationService.GetMyIpAsync();
            if (!string.IsNullOrEmpty(ip))
            {
                IpAddressInput = ip;
            }
            else
            {
                SetStatus("Failed to retrieve IP address", isError: true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }
    }

    public async Task LookupIpAsync()
    {
        if (!System.Net.IPAddress.TryParse(IpAddressInput, out _))
        {
            SetStatus(Locale["StatusInvalidIP"], isError: true);
            return;
        }

        IsLoading = true;
        IsResultVisible = false;
        SetStatus(Locale["StatusFetching"], isWorking: true);

        _performanceService.StartOperation("IP_Lookup");
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            
            // Get geolocation data
            _performanceService.StartOperation("Geolocation");
            var geoResult = await _geolocationService.GetGeolocationAsync(IpAddressInput, cts.Token);
            _performanceService.EndOperation("Geolocation");

            if (geoResult?.Status != "success")
            {
                SetStatus(Locale["StatusError"] + (geoResult?.Message ?? "Unknown error"), isError: true);
                return;
            }

            GeolocationResult = geoResult;

            // Start concurrent secondary tasks
            var timeTask = _geolocationService.GetLocalTimeAsync(
                geoResult.Lat, geoResult.Lon, geoResult.Timezone, cts.Token);
            
            var flagTask = _geolocationService.GetCountryFlagAsync(
                geoResult.CountryCode, cts.Token);
            
            var threatTask = _threatService.GetThreatInfoAsync(
                geoResult.Query, AbuseIpDbApiKey, VirusTotalApiKey, cts.Token);

            // Wait for all tasks with timeout
            await Task.WhenAll(timeTask, flagTask, threatTask).ConfigureAwait(false);

            // Set results
            LocalTime = await timeTask;
            FlagImage = await flagTask;
            var threatResult = await threatTask;
            
            ThreatInfo = threatResult.Summary;
            ThreatScore = threatResult.Score;
            AbuseIpDbInfo = threatResult.AbuseIpDbInfo;
            VirusTotalInfo = threatResult.VirusTotalInfo;

            sw.Stop();
            LookupDuration = $"Lookup time: {sw.ElapsedMilliseconds} ms";
            _performanceService.RecordMetric("LookupDurationMs", sw.ElapsedMilliseconds);

            PerformanceMetrics = _performanceService.GetCurrentMetrics();
            IsResultVisible = true;
            SetStatus(Locale["StatusSuccess"], isSuccess: true);

            // Save to history (fire and forget)
            _ = SaveLookupToHistoryAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Lookup timed out.", isError: true);
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", isError: true);
        }
        finally
        {
            _performanceService.EndOperation("IP_Lookup");
            IsLoading = false;
        }
    }

    public string GetAllInfoAsText()
    {
        if (GeolocationResult == null) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"IP Address: {GeolocationResult.Query}");
        sb.AppendLine($"Location: {LocationString}");
        sb.AppendLine($"ISP: {GeolocationResult.Isp}");
        sb.AppendLine($"Coordinates: {CoordinatesString}");
        sb.AppendLine($"Timezone: {GeolocationResult.Timezone}");
        sb.AppendLine($"Local Time: {LocalTime}");
        sb.AppendLine($"Threat Score: {ThreatScore}");
        return sb.ToString();
    }

    // Private helper methods

    private void SetStatus(string message, bool isSuccess = false, bool isError = false, bool isWorking = false)
    {
        StatusMessage = message;
        
        if (isSuccess)
            StatusBrush = Brushes.Green;
        else if (isError)
            StatusBrush = Brushes.Red;
        else if (isWorking)
            StatusBrush = Brushes.Blue;
        else
            StatusBrush = Brushes.Gray;
    }

    private void UpdateLocale()
    {
        var langCode = SelectedLanguageIndex == 1 ? "vi" : "en";
        Locale = _locales[langCode];
        OnPropertyChanged(nameof(Locale));
        SetStatus(Locale["StatusReady"]);
    }

    private async Task SaveLookupToHistoryAsync()
    {
        if (GeolocationResult == null) return;

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
            System.Diagnostics.Debug.WriteLine($"Failed to save history: {ex.Message}");
        }
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
