# IP GeoLocator - Project Overview for Gemini

This document provides a comprehensive overview of the `IP GeoLocator` project, intended for AI analysis and understanding.

## 1. Project Purpose

`IP GeoLocator` is a cross-platform desktop application built using .NET 9 and Avalonia UI. Its primary function is to provide detailed geolocation information for IP addresses, integrate with threat intelligence services, and offer a suite of network diagnostic tools. The application emphasizes performance, user experience, and extensibility.

## 2. Key Features

*   **IP Geolocation**: Retrieves geographic data (country, city, coordinates, ISP, timezone) for any given IP address.
*   **Threat Intelligence Integration**: Checks IP reputation using services like AbuseIPDB and VirusTotal (requires API keys).
*   **Local Time Lookup**: Displays the current local time for the IP's detected location.
*   **Lookup History**: Persists and allows searching of past IP lookups using an SQLite database.
*   **Data Export**: Supports exporting lookup results to JSON, CSV, and TXT formats.
*   **Multi-language Support**: Provides a localized user interface (English and Vietnamese).
*   **Theming**: Offers both dark and light UI themes.
*   **Performance Optimizations**:
    *   **Intelligent Caching**: Reduces redundant API calls with an expiration mechanism.
    *   **Concurrent API Processing**: Executes multiple API calls in parallel for faster results.
    *   **Optimized HTTP Client**: Utilizes connection pooling and proper timeout management.
    *   **Graceful Degradation**: Prevents slow services from blocking the entire lookup process.
*   **Network Tools Suite**: Includes functionalities like Pinger, Port Scanner, Tracerouter, DNS Lookup, Subnet Calculator, and Whois Lookup.
*   **Interactive World Map**: Visualizes IP locations on a world map.

## 3. Technology Stack

*   **Framework**: .NET 9
*   **UI Framework**: Avalonia UI (cross-platform, XAML-based)
*   **Database**: Entity Framework Core with SQLite
*   **API Integrations**:
    *   Geolocation: ip-api.com
    *   Threat Intelligence: AbuseIPDB, VirusTotal
    *   Time Services: timeapi.io, worldtimeapi.org
*   **Serialization**: System.Text.Json, Newtonsoft.Json
*   **Architecture**: Model-View-ViewModel (MVVM)

## 4. Project Architecture and Structure

The project adheres to the MVVM pattern, promoting separation of concerns and testability.

```
IPGeoLocator/
├── App.axaml                   # Application-wide XAML resources and styling
├── App.axaml.cs                # Application startup logic, theme management, and service registration
├── Program.cs                  # Main entry point for the .NET application
├── IPGeoLocator.csproj         # Project file (defines dependencies, target framework, etc.)
├── app.manifest                # Application manifest file

├── Controls/                   # Reusable custom Avalonia UI controls
│   ├── ThreatIndicatorControl.axaml
│   ├── ThreatIndicatorControl.axaml.cs
│   ├── ThreatVisualizationControl.axaml
│   ├── ThreatVisualizationControl.axaml.cs
│   ├── WorldMapControl.axaml
│   └── WorldMapControl.axaml.cs

├── Data/                       # Database context and configuration
│   └── AppDbContext.cs         # Entity Framework Core DbContext for SQLite

├── Models/                     # Plain Old C# Objects (POCOs) representing data structures
│   ├── IpScanResult.cs         # Model for IP lookup results
│   └── LookupHistory.cs        # Model for storing lookup history entries

├── NetworkTools/               # Module for various network diagnostic utilities
│   ├── Models/                 # Data models specific to network tools
│   ├── Scanners/               # Implementations of network scanning functionalities
│   │   ├── Pinger.cs
│   │   ├── PortScanner.cs
│   │   └── Tracerouter.cs
│   ├── Utils/                  # Utility classes for network operations
│   │   ├── DnsLookup.cs
│   │   ├── SubnetCalculator.cs
│   │   ├── SubnetInfo.cs
│   │   └── WhoisLookup.cs
│   ├── ViewModels/             # ViewModel for the Network Tools section
│   │   └── NetworkToolsViewModel.cs
│   └── Views/                  # View for the Network Tools section
│       ├── NetworkToolsView.axaml
│       └── NetworkToolsView.axaml.cs

├── Services/                   # Business logic, API interactions, and data management
│   ├── LookupHistoryService.cs # Manages saving and retrieving lookup history
│   ├── PerformanceService.cs   # Handles performance monitoring and optimization (e.g., caching)
│   └── ThreatIntelligenceService.cs # Orchestrates calls to threat intelligence APIs

├── Styles/                     # Application-wide UI styles and themes
│   └── CustomStyles.axaml      # Custom styles and resource dictionaries

├── ViewModels/                 # ViewModels responsible for UI logic and data binding
│   ├── HistoryViewModel.cs     # ViewModel for the lookup history view
│   ├── IpLookupViewModel.cs    # ViewModel for the main IP lookup functionality
│   ├── IpRangeScanViewModel.cs # ViewModel for IP range scanning
│   ├── MainViewModel.cs        # Main application ViewModel, orchestrating other ViewModels
│   ├── NetworkToolsViewModel.cs # (Also listed under NetworkTools/)
│   └── WorldMapViewModel.cs    # ViewModel for the world map visualization

├── Views/                      # Avalonia UI views (XAML files)
│   ├── HistoryView.axaml       # View for displaying lookup history
│   ├── HistoryView.axaml.cs
│   ├── IpLookupView.axaml      # Main view for IP lookup
│   ├── IpLookupView.axaml.cs
│   ├── IpRangeScannerWindow.axaml # Window for IP range scanning
│   ├── IpRangeScannerWindow.axaml.cs
│   ├── MainWindow.axaml        # Main application window
│   ├── MainWindow.axaml.cs
│   ├── WorldMapView.axaml      # View for the world map
│   ├── WorldMapView.axaml.cs
│   ├── WorldMapWindow.axaml    # Window for the world map
│   └── WorldMapWindow.axaml.cs

# Other files:
├── .gitattributes
├── .gitignore
├── CHANGELOG.md
├── FINAL_IMPLEMENTATION_REPORT.md
├── FINAL_IMPLEMENTATION_SUMMARY.md
├── gcov2html_fixed.py
├── gcov2html.py
├── GITHUB_ACTIONS_SETUP.md
├── IMPLEMENTATION_COMPLETION_REPORT.md
├── IMPLEMENTATION_SUMMARY.md
├── LICENSE
├── README.md
├── TEST_SUMMARY.md
├── verify_implementation.sh
```

## 5. Core Logic Flow (IP Lookup Example)

1.  **User Input**: An IP address is entered into `IpLookupView.axaml` and bound to `IpLookupViewModel.cs`.
2.  **Command Execution**: The "Lookup" button triggers a command in `IpLookupViewModel`, which then calls relevant methods in `Services/`.
3.  **Service Orchestration**: `ThreatIntelligenceService.cs` and other services make concurrent API calls to external providers (e.g., ip-api.com, AbuseIPDB, VirusTotal). `PerformanceService.cs` manages caching and HTTP client optimizations.
4.  **Data Aggregation**: Results from various APIs are aggregated and mapped to `IpScanResult.cs` model.
5.  **History Management**: `LookupHistoryService.cs` saves the `IpScanResult` to the SQLite database via `AppDbContext.cs`.
6.  **UI Update**: The `IpLookupViewModel` updates its observable properties, which are then reflected in `IpLookupView.axaml` and potentially `ThreatVisualizationControl.axaml` or `WorldMapControl.axaml`.

## 6. Potential Areas for Further Analysis/Development

*   **API Key Management**: Investigate secure storage and retrieval of API keys.
*   **Error Handling**: Review and enhance error handling across all API integrations and network tools.
*   **Unit/Integration Tests**: Expand test coverage for core services and view models.
*   **Performance Benchmarking**: Detailed benchmarking of concurrent API calls and caching mechanisms.
*   **UI/UX Enhancements**: Further refinement of the user interface and experience.
*   **Localization**: Ensure all UI elements are properly localized.
