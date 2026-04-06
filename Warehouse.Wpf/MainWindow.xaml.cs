using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using Warehouse.Wpf.Models;
using Warehouse.Wpf.Services;
using Warehouse.Wpf.ViewModels;

namespace Warehouse.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://localhost:7043/")
            };

            _viewModel = new MainViewModel(new WarehouseApiClient(httpClient));
            DataContext = _viewModel;

            Loaded += async (_, _) => await _viewModel.LoadAsync();
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