using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using IPGeoLocator.Controls;
using IPGeoLocator.Models;
using IPGeoLocator.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IPGeoLocator
{
    public partial class WorldMapWindow : Window
    {
        private WorldMapViewModel _viewModel;
        private WorldMapControl? _worldMapControl;
        private Button? _loadDataButton;
        private Button? _refreshButton;
        private Button? _clearButton;
        private Button? _exportMapButton;
        private Button? _closeButton;
        private CheckBox? _showThreatsOnlyCheckBox;
        private Slider? _threatThresholdSlider;
        private TextBlock? _statusTextBlock;
        private TextBlock? _thresholdValueTextBlock;

        public WorldMapWindow()
        {
            _viewModel = new WorldMapViewModel();
            InitializeComponent();
            SetupControls();
            SubscribeToEvents();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _worldMapControl = this.FindControl<WorldMapControl>("WorldMapControl");
            _loadDataButton = this.FindControl<Button>("LoadDataButton");
            _refreshButton = this.FindControl<Button>("RefreshButton");
            _clearButton = this.FindControl<Button>("ClearButton");
            _exportMapButton = this.FindControl<Button>("ExportMapButton");
            _closeButton = this.FindControl<Button>("CloseButton");
            _showThreatsOnlyCheckBox = this.FindControl<CheckBox>("ShowThreatsOnlyCheckBox");
            _threatThresholdSlider = this.FindControl<Slider>("ThreatThresholdSlider");
            _statusTextBlock = this.FindControl<TextBlock>("StatusTextBlock");
            _thresholdValueTextBlock = this.FindControl<TextBlock>("ThresholdValueTextBlock");
            
            // Bind the view model to the window
            DataContext = _viewModel;
        }

        private void SetupControls()
        {
            // Set up data binding for UI elements
            if (_statusTextBlock != null)
            {
                _statusTextBlock.Bind(TextBlock.TextProperty, 
                    new Avalonia.Data.Binding("StatusMessage"));
            }
            
            // Just set up the binding in a way compatible with Avalonia
            if (_threatThresholdSlider != null && _thresholdValueTextBlock != null)
            {
                // Using direct property binding
            }
        }

        private void SubscribeToEvents()
        {
            // Button click events
            if (_loadDataButton != null)
            {
                _loadDataButton.Click += async (sender, e) => await LoadDataAsync();
            }
            
            if (_refreshButton != null)
            {
                _refreshButton.Click += async (sender, e) => await RefreshMapAsync();
            }
            
            if (_clearButton != null)
            {
                _clearButton.Click += (sender, e) => ClearMap();
            }
            
            if (_exportMapButton != null)
            {
                _exportMapButton.Click += async (sender, e) => await ExportMapAsync();
            }
            
            if (_closeButton != null)
            {
                _closeButton.Click += (sender, e) => this.Close();
            }
            
            // Checkbox and slider events
            if (_showThreatsOnlyCheckBox != null)
            {
                _showThreatsOnlyCheckBox.PropertyChanged += (sender, e) =>
                {
                    UpdateMapVisualization();
                };
            }
            
            if (_threatThresholdSlider != null)
            {
                _threatThresholdSlider.PropertyChanged += (sender, e) =>
                {
                    UpdateMapVisualization();
                };
            }
            
            // ViewModel property change events
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.MapPoints) ||
                    e.PropertyName == nameof(_viewModel.ShowOnlyThreats) ||
                    e.PropertyName == nameof(_viewModel.ZoomLevel))
                {
                    UpdateMapVisualization();
                }
                else if (e.PropertyName == nameof(_viewModel.IsLoading))
                {
                    UpdateButtonStates();
                }
            };
            
            UpdateButtonStates(); // Initialize button states
        }

        private async Task LoadDataAsync()
        {
            // For now, we'll load sample data, but in a real implementation,
            // this would load from the history database or accept a list of IP locations
            var sampleLocations = new List<IpLocation>
            {
                new IpLocation { IpAddress = "8.8.8.8", Latitude = 37.751, Longitude = -97.822, Country = "United States", City = "New York", ThreatScore = 5 },
                new IpLocation { IpAddress = "1.1.1.1", Latitude = -37.098, Longitude = 145.363, Country = "Australia", City = "Melbourne", ThreatScore = 90 },
                new IpLocation { IpAddress = "114.114.114.114", Latitude = 39.9042, Longitude = 116.4074, Country = "China", City = "Beijing", ThreatScore = 75 },
                new IpLocation { IpAddress = "208.67.222.222", Latitude = 45.515, Longitude = -122.679, Country = "United States", City = "Portland", ThreatScore = 10 }
            };
            
            await _viewModel.LoadMapDataAsync(sampleLocations);
        }

        private async Task RefreshMapAsync()
        {
            _viewModel.RefreshCommand?.Execute(null);
            await Task.CompletedTask; // Keep as async method
        }

        private void ClearMap()
        {
            _viewModel.ClearMapAction();
            UpdateMapVisualization();
        }

        private async Task ExportMapAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Export Map",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("JPEG Image") { Patterns = new[] { "*.jpg", "*.jpeg" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("PDF Document") { Patterns = new[] { "*.pdf" } }
                    }
                });

                if (file != null)
                {
                    await _viewModel.ExportMapAsync(file.Path.LocalPath);
                }
            }
            catch (Exception ex)
            {
                // Show a simple message box for error
                var messageBox = new Window
                {
                    Title = "Export Error",
                    Width = 400,
                    Height = 200,
                    Content = new TextBlock 
                    { 
                        Text = $"Failed to export map: {ex.Message}",
                        Margin = new Thickness(10),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };
                await messageBox.ShowDialog(this);
            }
        }

        private void UpdateMapVisualization()
        {
            if (_worldMapControl == null || _viewModel.MapPoints == null) return;

            // Convert ViewModel MapPoints to the format expected by the control
            var points = _viewModel.MapPoints.Select(vp => new MapPoint
            {
                Latitude = vp.Latitude,
                Longitude = vp.Longitude,
                IpAddress = vp.IpAddress,
                Country = vp.Country,
                City = vp.City,
                ThreatScore = vp.ThreatScore,
                IsMalicious = vp.IsMalicious
            }).ToList();

            // Check if we should show only threats
            var showOnlyThreats = _viewModel.ShowOnlyThreats || 
                                  (_showThreatsOnlyCheckBox?.IsChecked == true);

            var threshold = _viewModel.ZoomLevel * 10; // Use zoom level as threshold placeholder
            if (_threatThresholdSlider != null)
            {
                threshold = _threatThresholdSlider.Value;
            }

            _worldMapControl.UpdateMap(points, showOnlyThreats, threshold);
        }

        private void UpdateButtonStates()
        {
            var isLoading = _viewModel.IsLoading;
            
            if (_loadDataButton != null)
                _loadDataButton.IsEnabled = !isLoading;
            if (_refreshButton != null)
                _refreshButton.IsEnabled = !isLoading;
            if (_exportMapButton != null)
                _exportMapButton.IsEnabled = !isLoading && _viewModel.MapPoints.Count > 0;
        }
        
        // Method to add a single IP location to the map
        public void AddIpLocation(IpLocation location)
        {
            var locations = new List<IpLocation> { location };
            _ = _viewModel.LoadMapDataAsync(locations); // Fire and forget
        }
    }
}