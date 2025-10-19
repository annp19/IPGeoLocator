using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using IPGeoLocator.Models;
using IPGeoLocator.Services;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace IPGeoLocator
{
    public partial class HistoryWindow : Window
    {
        private readonly LookupHistoryService _historyService;
        private ObservableCollection<HistoryItemViewModel> _historyItems;
        private DataGrid? _dataGrid;
        private TextBox? _searchBox;
        private Button? _searchButton;
        private Button? _refreshButton;
        private Button? _clearButton;
        private Button? _closeButton;
        private TextBlock? _resultsCountText;

        public HistoryWindow(LookupHistoryService historyService)
        {
            _historyService = historyService;
            _historyItems = new ObservableCollection<HistoryItemViewModel>();
            
            InitializeComponent();
            SetupControls();
            LoadHistoryAsync();
            SubscribeToEvents();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _dataGrid = this.FindControl<DataGrid>("HistoryDataGrid");
            _searchBox = this.FindControl<TextBox>("SearchBox");
            _searchButton = this.FindControl<Button>("SearchButton");
            _refreshButton = this.FindControl<Button>("RefreshButton");
            _clearButton = this.FindControl<Button>("ClearButton");
            _closeButton = this.FindControl<Button>("CloseButton");
            _resultsCountText = this.FindControl<TextBlock>("ResultsCountText");
            
            _dataGrid.DoubleTapped += OnItemDoubleTapped;
        }

        private void SetupControls()
        {
            _dataGrid.ItemsSource = _historyItems;
        }

        private void SubscribeToEvents()
        {
            _searchButton.Click += async (sender, e) => await SearchHistoryAsync();
            _refreshButton.Click += async (sender, e) => await RefreshHistoryAsync();
            _clearButton.Click += async (sender, e) => await ClearHistoryAsync();
            _closeButton.Click += (sender, e) => this.Close();
            
            // Search on Enter key in search box
            _searchBox.KeyUp += async (sender, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Enter)
                {
                    await SearchHistoryAsync();
                }
            };
        }

        private async Task LoadHistoryAsync()
        {
            try
            {
                var lookups = await _historyService.GetAllLookupsAsync();
                ResetPagination(); // Reset pagination when loading new data
                UpdateHistoryDisplay(lookups);
            }
            catch (Exception ex)
            {
                // Simple error reporting - could be improved with proper UI element
                System.Diagnostics.Debug.WriteLine($"Failed to load history: {ex.Message}");
                _resultsCountText.Text = $"Error loading history: {ex.Message}";
            }
        }

        private async Task SearchHistoryAsync()
        {
            string searchTerm = _searchBox.Text?.Trim() ?? "";
            try
            {
                var lookups = string.IsNullOrEmpty(searchTerm) 
                    ? await _historyService.GetAllLookupsAsync()
                    : await _historyService.SearchLookupsAsync(searchTerm);
                
                ResetPagination(); // Reset pagination when searching
                UpdateHistoryDisplay(lookups);
            }
            catch (Exception ex)
            {
                // Simple error reporting - could be improved with proper UI element
                System.Diagnostics.Debug.WriteLine($"Failed to search history: {ex.Message}");
                _resultsCountText.Text = $"Error searching history: {ex.Message}";
            }
        }

        private async Task RefreshHistoryAsync()
        {
            await LoadHistoryAsync();
        }

        private async Task ClearHistoryAsync()
        {
            // For now, we'll skip the confirmation to avoid additional dependencies
            // In production, you'd want to implement a proper confirmation dialog
            try
            {
                await _historyService.ClearAllLookupsAsync();
                _historyItems.Clear();
                _resultsCountText.Text = $"No records found";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear history: {ex.Message}");
                _resultsCountText.Text = $"Error clearing history: {ex.Message}";
            }
        }

        // Pagination settings for large datasets to improve UI responsiveness
        private const int PageSize = 50; // Show 50 records per page
        private int _currentPage = 0;
        
        private void UpdateHistoryDisplay(System.Collections.Generic.List<LookupHistory> lookups)
        {
            // For large datasets, implement pagination to improve UI responsiveness
            var totalCount = lookups.Count;
            
            // If we have more records than the page size, implement pagination
            if (totalCount > PageSize)
            {
                var startIndex = _currentPage * PageSize;
                var endIndex = Math.Min(startIndex + PageSize, totalCount);
                var pageItems = lookups.AsEnumerable().Skip(startIndex).Take(endIndex - startIndex).ToList();
                
                _historyItems.Clear();
                
                foreach (var lookup in pageItems)
                {
                    _historyItems.Add(new HistoryItemViewModel(lookup));
                }
                
                // Update results count with pagination info
                _resultsCountText.Text = $"{pageItems.Count} records shown (Page {_currentPage + 1} of {(int)Math.Ceiling((double)totalCount / PageSize)})";
            }
            else
            {
                // For smaller datasets, show all items
                _historyItems.Clear();
                
                foreach (var lookup in lookups)
                {
                    _historyItems.Add(new HistoryItemViewModel(lookup));
                }
                
                int count = _historyItems.Count;
                _resultsCountText.Text = count == 1 
                    ? $"{count} record found" 
                    : $"{count} records found";
            }
        }
        
        // Navigation methods for pagination
        private void NavigateToPreviousPage(System.Collections.Generic.List<LookupHistory> allLookups)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                UpdateHistoryDisplay(allLookups);
            }
        }
        
        private void NavigateToNextPage(System.Collections.Generic.List<LookupHistory> allLookups)
        {
            var totalPages = Math.Ceiling((double)allLookups.Count / PageSize);
            if (_currentPage < totalPages - 1)
            {
                _currentPage++;
                UpdateHistoryDisplay(allLookups);
            }
        }
        
        // Reset pagination when loading new data
        private void ResetPagination()
        {
            _currentPage = 0;
        }

        private void OnItemDoubleTapped(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_dataGrid.SelectedItem is HistoryItemViewModel selectedItem)
            {
                // Use a callback to pass the selected IP back to the main window
                SelectedIpCallback?.Invoke(selectedItem.IpAddress);
                this.Close();
            }
        }
        
        // Add a callback property to communicate back to the main window
        public Action<string>? SelectedIpCallback { get; set; }
    }
    
    public class HistoryItemViewModel
    {
        private readonly LookupHistory _lookup;
        
        public HistoryItemViewModel(LookupHistory lookup)
        {
            _lookup = lookup;
        }
        
        public string IpAddress => _lookup.IpAddress;
        public string CityAndCountry => string.IsNullOrEmpty(_lookup.City) 
            ? (_lookup.Country ?? "") 
            : $"{_lookup.City}, {_lookup.Country}";
        public string Isp => _lookup.Isp ?? "N/A";
        public string ThreatScoreDisplay => _lookup.ThreatScore?.ToString() ?? "N/A";
        public string LookupTimeDisplay => _lookup.LookupTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string LookupDuration => _lookup.LookupDuration ?? "N/A";
    }
}