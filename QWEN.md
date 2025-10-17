# IP Geolocation Tool (IPGeoLocator)

## Project Overview

IPGeoLocator is a cross-platform desktop application built with .NET 9 and Avalonia UI framework. It provides IP address geolocation services, allowing users to look up geographic information about IP addresses including location, ISP, coordinates, timezone, and threat intelligence scores.

### Key Features:
- **IP Geolocation**: Lookup geographic information for any IP address
- **Threat Intelligence**: Check IP reputation using AbuseIPDB API (requires API key)
- **Local Time Lookup**: Get current local time for the IP's location
- **Country Flags**: Display country flags for geolocated IPs
- **Multi-language Support**: English and Vietnamese UI
- **Dark/Light Theme**: Theme switching support
- **IP Retrieval**: "Get My IP" functionality to retrieve your own IP address
- **Data Export**: Copy geolocation results to clipboard

### Technologies Used:
- **Framework**: .NET 9
- **UI Framework**: Avalonia 11.1.0 (cross-platform UI)
- **APIs**:
  - ip-api.com for geolocation data
  - flagcdn.com for country flags
  - timeapi.io and worldtimeapi.org for timezone/local time
  - AbuseIPDB API for threat intelligence (optional)
- **HTTP Client**: System.Net.Http.HttpClient with timeout configuration
- **JSON Processing**: System.Text.Json

## Building and Running

### Prerequisites
- .NET 9 SDK or later
- Git (for cloning the repository)

### Building the Project
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

### Using VSCode Tasks
The project includes VSCode tasks for common operations:
- `build` - Build the project
- `publish` - Publish the project
- `watch` - Run the project in watch mode

To use VSCode tasks:
1. Open the project in VSCode
2. Press `Ctrl+Shift+P` and type "Tasks: Run Task"
3. Select the desired task (build, publish, or watch)

### Publishing
To create a standalone executable:
```bash
dotnet publish -c Release
```

## Development Conventions

### Architecture
- **MVVM Pattern**: Uses Model-View-ViewModel pattern with Avalonia
- **Async Programming**: All network requests use async/await with proper cancellation tokens
- **Caching**: Implements in-memory caching for geolocation data, flags, and local time lookups
- **INotifyPropertyChanged**: Implemented for data binding in the MainWindow class

### Code Structure
- **MainWindow.axaml**: Avalonia XAML UI definition
- **MainWindow.axaml.cs**: Main application logic with all functionality
- **App.axaml.cs**: Application startup and initialization
- **Program.cs**: Entry point and Avalonia application configuration
- **Models**: GeolocationResponse and TimeApiResponse records for JSON deserialization

### API Integration
- All API calls have an 8-second timeout
- Uses a single static HttpClient instance for the application lifetime
- Parallel execution for dependent API calls to reduce lookup time
- Fallback mechanisms for time API calls (multiple services attempted)

### Error Handling
- Timeout handling for network requests
- Graceful fallback for missing country flags
- Proper error status messages to the UI
- Exception handling for all network operations

### UI Features
- Responsive design with minimum window size constraints
- Loading indicators during API requests
- Color-coded status messages (success, error, working)
- Copy functionality for results
- Localization support with English and Vietnamese

### Special Tools
The project includes two Python scripts for code coverage visualization:
- `gcov2html.py`: Converts gcov coverage files to HTML reports with modern UI
- `gcov2html_fixed.py`: Fixed version of the coverage report generator
- These tools are for C/C++ coverage analysis (not directly related to this C# project)

## Project Structure
```
IPGeoLocator/
├── App.axaml              # Application XAML definition
├── App.axaml.cs           # Application startup logic
├── MainWindow.axaml       # Main window XAML definition
├── MainWindow.axaml.cs    # Main application logic and UI handling
├── Program.cs             # Entry point
├── IPGeoLocator.csproj    # Project configuration
├── app.manifest           # Application manifest
├── gcov2html.py           # Python script to convert gcov to HTML
├── gcov2html_fixed.py     # Fixed version of gcov HTML converter
├── .vscode/
│   ├── launch.json        # Debug configurations
│   └── tasks.json         # Build tasks
├── bin/                   # Build output
├── obj/                   # Intermediate build files
└── Styles/                # Style definitions
```

## Configuration
The application does not require configuration files for basic operation, but threat intelligence features require an AbuseIPDB API key to be entered in the UI.

## Testing
To run any tests if available:
```bash
dotnet test
```

## Contributing
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request