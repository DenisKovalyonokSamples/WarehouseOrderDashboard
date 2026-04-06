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
    public partial class MainWindow : Window
    {
        private static readonly Uri DefaultApiBaseAddress = new("https://localhost:58291/");
        private readonly MainViewModel _viewModel;

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

        private static Uri ResolveApiBaseAddress()
        {
            const string apiBaseAddressEnvironmentVariable = "WAREHOUSE_API_BASE_URL";
            var fromEnvironment = Environment.GetEnvironmentVariable(apiBaseAddressEnvironmentVariable);
            if (Uri.TryCreate(fromEnvironment, UriKind.Absolute, out var environmentUri))
            {
                return EnsureTrailingSlash(environmentUri);
            }

            var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                using var stream = File.OpenRead(appSettingsPath);
                using var json = JsonDocument.Parse(stream);

                if (json.RootElement.TryGetProperty("Api", out var apiElement)
                    && apiElement.TryGetProperty("BaseAddress", out var baseAddressElement)
                    && Uri.TryCreate(baseAddressElement.GetString(), UriKind.Absolute, out var configUri))
                {
                    return EnsureTrailingSlash(configUri);
                }
            }

            return DefaultApiBaseAddress;
        }

        private static Uri EnsureTrailingSlash(Uri uri)
        {
            var value = uri.ToString();
            if (!value.EndsWith('/'))
            {
                value += "/";
            }

            return new Uri(value, UriKind.Absolute);
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