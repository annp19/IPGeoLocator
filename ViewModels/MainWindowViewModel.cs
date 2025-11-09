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
    // ... (giữ nguyên mọi mã logic khác, chỉ sửa type property là Models.GeolocationResponse)
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
    // ... (còn lại giữ nguyên logic, vẫn dùng Models.GeolocationResponse, không có ViewModels.GeolocationResponse)
}
