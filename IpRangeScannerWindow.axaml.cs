using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using IPGeoLocator.ViewModels;
using System;
using System.Threading.Tasks;

namespace IPGeoLocator
{
    public partial class IpRangeScannerWindow : Window
    {
        private IpRangeScannerViewModel _viewModel;
        private TextBox? _ipRangeTextBox;
        private Button? _startScanButton;
        private Button? _stopScanButton;
        private Button? _exportResultsButton;
        private Button? _importFileButton;
        private Button? _cancelButton;
        private ProgressBar? _scanProgressBar;
        private TextBlock? _progressTextBlock;
        private DataGrid? _resultsDataGrid;
        private CheckBox? _includeThreatInfoCheckBox;
        
        // Properties to accept API keys from parent window
        public string AbuseIpDbApiKey { get; set; } = "";
        public string VirusTotalApiKey { get; set; } = "";

        public IpRangeScannerWindow()
        {
            _viewModel = new IpRangeScannerViewModel();
            InitializeComponent();
            SetupControls();
            SubscribeToEvents();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _ipRangeTextBox = this.FindControl<TextBox>("IpRangeTextBox");
            _startScanButton = this.FindControl<Button>("StartScanButton");
            _stopScanButton = this.FindControl<Button>("StopScanButton");
            _exportResultsButton = this.FindControl<Button>("ExportResultsButton");
            _importFileButton = this.FindControl<Button>("ImportFileButton");
            _cancelButton = this.FindControl<Button>("CancelButton");
            _scanProgressBar = this.FindControl<ProgressBar>("ScanProgressBar");
            _progressTextBlock = this.FindControl<TextBlock>("ProgressTextBlock");
            _resultsDataGrid = this.FindControl<DataGrid>("ResultsDataGrid");
            _includeThreatInfoCheckBox = this.FindControl<CheckBox>("IncludeThreatInfoCheckBox");
            
            // Bind the view model to the window
            DataContext = _viewModel;
        }

        private void SetupControls()
        {
            // Set up data binding for UI elements
            if (_scanProgressBar != null)
            {
                _scanProgressBar.Bind(ProgressBar.ValueProperty, 
                    new Avalonia.Data.Binding("CurrentProgress"));
                _scanProgressBar.Bind(ProgressBar.MaximumProperty, 
                    new Avalonia.Data.Binding("TotalProgress"));
            }
            
            if (_progressTextBlock != null)
            {
                _progressTextBlock.Bind(TextBlock.TextProperty, 
                    new Avalonia.Data.Binding("StatusMessage"));
            }
            
            if (_resultsDataGrid != null)
            {
                _resultsDataGrid.ItemsSource = _viewModel.ScanResults;
            }
            
            if (_ipRangeTextBox != null)
            {
                _ipRangeTextBox.Bind(TextBox.TextProperty, 
                    new Avalonia.Data.Binding("IpRange"));
            }
            
            if (_includeThreatInfoCheckBox != null)
            {
                _includeThreatInfoCheckBox.Bind(CheckBox.IsCheckedProperty, 
                    new Avalonia.Data.Binding("IncludeThreatInfo"));
            }
        }

        private void SubscribeToEvents()
        {
            // Button click events
            if (_startScanButton != null)
            {
                _startScanButton.Click += async (sender, e) => await StartScanAsync();
            }
            
            if (_stopScanButton != null)
            {
                _stopScanButton.Click += (sender, e) => _viewModel.StopScan();
            }
            
            if (_exportResultsButton != null)
            {
                _exportResultsButton.Click += async (sender, e) => await ExportResultsAsync();
            }
            
            if (_importFileButton != null)
            {
                _importFileButton.Click += async (sender, e) => await ImportIpListAsync();
            }
            
            if (_cancelButton != null)
            {
                _cancelButton.Click += (sender, e) => this.Close();
            }
            
            // Update progress bar based on current progress vs total
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.CurrentProgress) || 
                    e.PropertyName == nameof(_viewModel.TotalProgress))
                {
                    UpdateProgressBar();
                }
                else if (e.PropertyName == nameof(_viewModel.IsScanning))
                {
                    UpdateButtonStates();
                }
            };
            
            UpdateButtonStates(); // Initialize button states
        }

        private async Task StartScanAsync()
        {
            await _viewModel.StartScanAsync(AbuseIpDbApiKey);
        }

        private async Task ImportIpListAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Import IP List",
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    },
                    AllowMultiple = false
                });

                if (files != null && files.Count > 0)
                {
                    await _viewModel.ImportIpListAsync(files[0].Path.LocalPath);
                }
            }
            catch (Exception ex)
            {
                // Show a simple message box for error
                var messageBox = new Window
                {
                    Title = "Import Error",
                    Width = 400,
                    Height = 200,
                    Content = new TextBlock 
                    { 
                        Text = $"Failed to import IP list: {ex.Message}",
                        Margin = new Thickness(10),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };
                await messageBox.ShowDialog(this);
            }
        }

        private async Task ExportResultsAsync()
        {
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.StorageProvider == null) return;

                var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Export Scan Results",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } }
                    }
                });

                if (file != null)
                {
                    await _viewModel.ExportResultsAsync(file.Path.LocalPath);
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
                        Text = $"Failed to export results: {ex.Message}",
                        Margin = new Thickness(10),
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                    }
                };
                await messageBox.ShowDialog(this);
            }
        }

        private void UpdateProgressBar()
        {
            if (_scanProgressBar != null && _viewModel.TotalProgress > 0)
            {
                // Calculate percentage for the progress bar
                var percentage = (double)_viewModel.CurrentProgress / _viewModel.TotalProgress * 100;
                _scanProgressBar.Value = _viewModel.CurrentProgress;
            }
        }

        private void UpdateButtonStates()
        {
            if (_startScanButton != null)
                _startScanButton.IsEnabled = !_viewModel.IsScanning;
            if (_stopScanButton != null)
                _stopScanButton.IsEnabled = _viewModel.IsScanning;
            if (_exportResultsButton != null)
                _exportResultsButton.IsEnabled = !_viewModel.IsScanning && _viewModel.ScanResults.Count > 0;
        }
    }
}