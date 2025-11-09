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
                // Show a dialog to let user choose the import file
                var dialog = new OpenFileDialog()
                {
                    Title = "Import IP List",
                    Filters = new System.Collections.Generic.List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "Text Files", Extensions = new System.Collections.Generic.List<string> { "txt" } },
                        new FileDialogFilter { Name = "CSV Files", Extensions = new System.Collections.Generic.List<string> { "csv" } },
                        new FileDialogFilter { Name = "All Files", Extensions = new System.Collections.Generic.List<string> { "*" } }
                    }
                };

                var result = await dialog.ShowAsync(this);
                if (result != null && result.Length > 0)
                {
                    await _viewModel.ImportIpListAsync(result[0]);
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
                // Show a dialog to let user choose the export format
                var dialog = new SaveFileDialog()
                {
                    Title = "Export Scan Results",
                    Filters = new System.Collections.Generic.List<FileDialogFilter>
                    {
                        new FileDialogFilter { Name = "JSON Files", Extensions = new System.Collections.Generic.List<string> { "json" } },
                        new FileDialogFilter { Name = "CSV Files", Extensions = new System.Collections.Generic.List<string> { "csv" } },
                        new FileDialogFilter { Name = "Text Files", Extensions = new System.Collections.Generic.List<string> { "txt" } }
                    }
                };

                var result = await dialog.ShowAsync(this);
                if (result != null)
                {
                    await _viewModel.ExportResultsAsync(result);
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