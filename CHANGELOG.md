# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Initial release of IP GeoLocator
- IP address geolocation lookup
- Threat intelligence integration (AbuseIPDB, VirusTotal)
- Local time lookup for IP addresses
- Lookup history with SQLite database
- Export functionality (JSON, CSV, TXT)
- Multi-language support (English, Vietnamese)
- Dark/Light theme support

### Changed
- Enhanced caching with expiration to reduce redundant API calls
- Optimized HTTP client configuration with connection pooling
- Improved concurrent API processing for faster lookups
- Added proper timeout management for better reliability
- Implemented graceful degradation for individual services
- Moved history saving to background tasks for better UI responsiveness

### Fixed
- Various performance bottlenecks in API calls
- Memory allocation issues in hot paths
- Timeout handling for slow services
- Error handling for failed API requests
- UI blocking during history operations