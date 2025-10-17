# IP GeoLocator

A cross-platform desktop application for IP address geolocation with threat intelligence capabilities.

## Features

- **IP Geolocation**: Look up geographic information for any IP address
- **Threat Intelligence**: Check IP reputation using multiple threat intelligence services
- **Local Time Lookup**: Get current local time for the IP's location
- **Lookup History**: Store and search previous lookups with SQLite database
- **Export Functionality**: Export results to JSON, CSV, and TXT formats
- **Multi-language Support**: English and Vietnamese UI
- **Dark/Light Theme**: Toggle between light and dark themes

## Performance Improvements

- **Enhanced Caching**: Intelligent caching with expiration to reduce redundant API calls
- **Concurrent API Processing**: All API calls run in parallel for faster lookups
- **Optimized HTTP Client**: Connection pooling and proper timeout management
- **Graceful Degradation**: Individual service timeouts prevent blocking on slow services

## Technologies Used

- .NET 9
- Avalonia UI Framework (cross-platform)
- Entity Framework Core (SQLite database)
- REST API Integration (ip-api.com, AbuseIPDB, VirusTotal, timeapi.io, worldtimeapi.org)
- JSON Serialization (System.Text.Json, Newtonsoft.Json)

## Getting Started

### Prerequisites

- .NET 9 SDK or later
- Git (for cloning the repository)

### Building and Running

```bash
# Clone the repository
git clone <repository-url>
cd IPGeoLocator

# Build the project
dotnet build

# Run the application
dotnet run

# Or run in watch mode for development
dotnet watch run
```

### Using Visual Studio Code Tasks

The project includes VSCode tasks for common operations:
- `build` - Build the project
- `publish` - Publish the project
- `watch` - Run the project in watch mode

## Usage

1. Enter an IP address in the input field
2. Click "Lookup" to retrieve geolocation information
3. View detailed information including location, ISP, coordinates, timezone, and local time
4. Check threat intelligence scores from AbuseIPDB and VirusTotal (requires API keys)
5. Use the "Copy All" button to copy all information to clipboard
6. Use the "Copy Coordinates" button to copy just the coordinates
7. View lookup history with the "History" button
8. Export results using the "Export Results" button

## Configuration

Some features require API keys:
- **AbuseIPDB API Key**: For enhanced threat intelligence
- **VirusTotal API Key**: For additional threat intelligence

API keys can be entered in the main application interface.

## Project Structure

```
IPGeoLocator/
├── App.axaml              # Application XAML definition
├── App.axaml.cs           # Application startup logic
├── MainWindow.axaml       # Main window XAML definition
├── MainWindow.axaml.cs   # Main application logic and UI handling
├── HistoryWindow.axaml    # History window XAML definition
├── HistoryWindow.axaml.cs # History window logic
├── Program.cs             # Entry point
├── IPGeoLocator.csproj    # Project configuration
├── app.manifest          # Application manifest
├── .gitignore            # Git ignore file
├── README.md             # This file
├── QWEN.md               # Project context documentation
├── Data/                 # Database context
├── Models/               # Data models
├── Services/             # Business logic services
├── Styles/               # Custom styles
├── bin/                  # Build output
└── obj/                  # Intermediate build files
```

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- ip-api.com for free geolocation API
- AbuseIPDB for threat intelligence API
- VirusTotal for malware analysis API
- flagcdn.com for country flag images
- timeapi.io and worldtimeapi.org for time zone information