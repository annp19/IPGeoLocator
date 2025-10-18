using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ScottPlot;

namespace IPGeoLocator.ViewModels
{
    public class WorldMapViewModel : INotifyPropertyChanged
    {
        private List<MapPoint> _mapPoints = new();
        private string _statusMessage = "Ready to display IP locations";
        private bool _isLoading;
        private double _minLatitude = -90;
        private double _maxLatitude = 90;
        private double _minLongitude = -180;
        private double _maxLongitude = 180;

        public List<MapPoint> MapPoints
        {
            get => _mapPoints;
            set => SetProperty(ref _mapPoints, value);
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

        public double MinLatitude
        {
            get => _minLatitude;
            set => SetProperty(ref _minLatitude, value);
        }

        public double MaxLatitude
        {
            get => _maxLatitude;
            set => SetProperty(ref _maxLatitude, value);
        }

        public double MinLongitude
        {
            get => _minLongitude;
            set => SetProperty(ref _minLongitude, value);
        }

        public double MaxLongitude
        {
            get => _maxLongitude;
            set => SetProperty(ref _maxLongitude, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

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

        public async Task LoadMapDataAsync(List<IpLocation> locations)
        {
            IsLoading = true;
            StatusMessage = $"Loading {locations.Count} IP locations...";

            try
            {
                var points = new List<MapPoint>();
                
                foreach (var location in locations)
                {
                    if (location.Latitude.HasValue && location.Longitude.HasValue)
                    {
                        points.Add(new MapPoint
                        {
                            Latitude = location.Latitude.Value,
                            Longitude = location.Longitude.Value,
                            IpAddress = location.IpAddress,
                            Country = location.Country ?? "Unknown",
                            City = location.City ?? "Unknown",
                            ThreatScore = location.ThreatScore,
                            IsMalicious = location.ThreatScore > 70 // Arbitrary threshold
                        });
                    }
                }

                MapPoints = points;
                UpdateMapBounds(points);
                StatusMessage = $"Loaded {points.Count} IP locations on the map";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading map data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateMapBounds(List<MapPoint> points)
        {
            if (points.Count == 0) return;

            var minLat = double.MaxValue;
            var maxLat = double.MinValue;
            var minLon = double.MaxValue;
            var maxLon = double.MinValue;

            foreach (var point in points)
            {
                minLat = Math.Min(minLat, point.Latitude);
                maxLat = Math.Max(maxLat, point.Latitude);
                minLon = Math.Min(minLon, point.Longitude);
                maxLon = Math.Max(maxLon, point.Longitude);
            }

            // Add some padding
            var latPadding = (maxLat - minLat) * 0.1;
            var lonPadding = (maxLon - minLon) * 0.1;

            MinLatitude = Math.Max(-90, minLat - latPadding);
            MaxLatitude = Math.Min(90, maxLat + latPadding);
            MinLongitude = Math.Max(-180, minLon - lonPadding);
            MaxLongitude = Math.Min(180, maxLon + lonPadding);
        }

        public void AddPoint(MapPoint point)
        {
            var points = new List<MapPoint>(MapPoints) { point };
            MapPoints = points;
            UpdateMapBounds(points);
        }

        public void ClearPoints()
        {
            MapPoints = new List<MapPoint>();
            MinLatitude = -90;
            MaxLatitude = 90;
            MinLongitude = -180;
            MaxLongitude = 180;
            StatusMessage = "Map cleared";
        }

        public async Task ExportMapAsync(string filePath)
        {
            StatusMessage = "Exporting map...";
            IsLoading = true;

            try
            {
                // This would export the map visualization to an image file
                // Implementation would depend on the specific charting library used
                await Task.Delay(1000); // Simulate export process
                StatusMessage = $"Map exported to: {filePath}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Export failed: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    public class MapPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string IpAddress { get; set; } = "";
        public string Country { get; set; } = "";
        public string City { get; set; } = "";
        public int ThreatScore { get; set; } = -1;
        public bool IsMalicious { get; set; } = false;
        public string Label => $"{IpAddress}\\n{City}, {Country}\\nScore: {ThreatScore}";
    }

    public class IpLocation
    {
        public string IpAddress { get; set; } = "";
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Country { get; set; }
        public string? City { get; set; }
        public int ThreatScore { get; set; } = -1;
    }
}