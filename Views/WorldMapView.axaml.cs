using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using IPGeoLocator.ViewModels;
using IPGeoLocator.Controls;
using System;
using System.Collections.Generic;

namespace IPGeoLocator.Views
{
    public partial class WorldMapView : Window
    {
        private readonly WorldMapViewModel _viewModel;

        public WorldMapView()
        {
            InitializeComponent();
            _viewModel = new WorldMapViewModel();
            DataContext = _viewModel;
            
            // Subscribe to property changes to update the map visualization
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.MapPoints) || 
                    e.PropertyName == nameof(_viewModel.ShowOnlyThreats))
                {
                    UpdateMapVisualization();
                }
            };
            
            // Initial map update
            UpdateMapVisualization();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        public void UpdateMapVisualization()
        {
            var worldMapControl = this.FindControl<WorldMapControl>("WorldMapControl");
            if (worldMapControl != null && _viewModel != null)
            {
                worldMapControl.UpdateMap(_viewModel.MapPoints, _viewModel.ShowOnlyThreats);
            }
        }

        // Method to load IP location data onto the map
        public async void LoadIpLocations(List<IpLocation> locations)
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadMapDataAsync(locations);
            }
        }

        // Method to add a single IP location to the map
        public void AddIpLocation(IpLocation location)
        {
            if (_viewModel != null && location.Latitude.HasValue && location.Longitude.HasValue)
            {
                var point = new MapPoint
                {
                    Latitude = location.Latitude.Value,
                    Longitude = location.Longitude.Value,
                    IpAddress = location.IpAddress,
                    Country = location.Country ?? "Unknown",
                    City = location.City ?? "Unknown",
                    ThreatScore = location.ThreatScore,
                    IsMalicious = location.ThreatScore > 70
                };
                
                _viewModel.AddPoint(point);
            }
        }

        // Method to clear all points from the map
        public void ClearMap()
        {
            _viewModel?.ClearPoints();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}