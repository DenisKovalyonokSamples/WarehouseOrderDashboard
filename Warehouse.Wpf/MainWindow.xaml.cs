using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Warehouse.Wpf.Models;
using Warehouse.Wpf.Services;
using Warehouse.Wpf.ViewModels;

namespace Warehouse.Wpf
{
    /// <summary>
    /// Main application window.
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly Uri DefaultApiBaseAddress = new("https://localhost:58291/");
        private readonly MainViewModel _viewModel;

        /// <summary>
        /// Initializes the main window and data context.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            var httpClient = new HttpClient
            {
                BaseAddress = ResolveApiBaseAddress()
            };

            _viewModel = new MainViewModel(new WarehouseApiClient(httpClient));
            DataContext = _viewModel;

            Loaded += async (_, _) => await _viewModel.LoadAsync();
        }

        /// <summary>
        /// Resolves API base address from environment, local appsettings, or default value.
        /// </summary>
        private static Uri ResolveApiBaseAddress()
        {
            const string apiBaseAddressEnvironmentVariableName = "WAREHOUSE_API_BASE_URL";
            var environmentApiBaseAddress = Environment.GetEnvironmentVariable(apiBaseAddressEnvironmentVariableName);
            if (Uri.TryCreate(environmentApiBaseAddress, UriKind.Absolute, out var environmentApiBaseAddressUri))
            {
                return EnsureTrailingSlash(environmentApiBaseAddressUri);
            }

            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                using var appSettingsStream = File.OpenRead(appSettingsPath);
                using var appSettingsJson = JsonDocument.Parse(appSettingsStream);

                if (appSettingsJson.RootElement.TryGetProperty("Api", out var apiElement)
                    && apiElement.TryGetProperty("BaseAddress", out var baseAddressElement)
                    && Uri.TryCreate(baseAddressElement.GetString(), UriKind.Absolute, out var configuredApiBaseAddressUri))
                {
                    return EnsureTrailingSlash(configuredApiBaseAddressUri);
                }
            }

            return DefaultApiBaseAddress;
        }

        /// <summary>
        /// Ensures URI text ends with a trailing slash.
        /// </summary>
        private static Uri EnsureTrailingSlash(Uri uri)
        {
            var uriText = uri.ToString();
            if (!uriText.EndsWith('/'))
            {
                uriText += "/";
            }

            return new Uri(uriText, UriKind.Absolute);
        }

        private void OrdersGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid grid)
            {
                return;
            }

            var selectedIds = grid.SelectedItems
                .OfType<OrderListItemDto>()
                .Select(x => x.Id)
                .ToArray();

            _viewModel.UpdateSelectedOrders(selectedIds);
        }
    }
}