using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Warehouse.Wpf.Infrastructure;
using Warehouse.Wpf.Models;
using Warehouse.Wpf.Services;

namespace Warehouse.Wpf.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IWarehouseApiClient _apiClient;
    private int _page = 1;
    private string? _searchText;
    private string _statusText = "Ready";
    private DashboardDto _dashboard = new();

    public MainViewModel(IWarehouseApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshOrdersCommand = new AsyncRelayCommand(RefreshOrdersAsync);
        RefreshStockCommand = new AsyncRelayCommand(RefreshStockAsync);
        RefreshDashboardCommand = new AsyncRelayCommand(RefreshDashboardAsync);
        CreatePickingTaskCommand = new AsyncRelayCommand(CreatePickingTaskAsync, () => SelectedOrderIds.Count > 0);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OrderListItemDto> Orders { get; } = [];
    public ObservableCollection<StockOverviewDto> Stock { get; } = [];
    public ObservableCollection<int> SelectedOrderIds { get; } = [];

    public ICommand RefreshOrdersCommand { get; }
    public ICommand RefreshStockCommand { get; }
    public ICommand RefreshDashboardCommand { get; }
    public ICommand CreatePickingTaskCommand { get; }

    public string? SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public DashboardDto Dashboard
    {
        get => _dashboard;
        private set
        {
            _dashboard = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DashboardSummary));
        }
    }

    public string DashboardSummary =>
        $"Orders Today: {Dashboard.TodayOrderCount} | Overdue Tasks: {Dashboard.OverdueTasks} | Unfulfilled Orders: {Dashboard.UnfulfilledOrders} | Avg min to picking start: {Dashboard.AvgMinutesFromCreateToPickingStart:F1}";

    public async Task LoadAsync()
    {
        await RefreshOrdersAsync();
        await RefreshStockAsync();
        await RefreshDashboardAsync();
    }

    public void UpdateSelectedOrders(IEnumerable<int> orderIds)
    {
        SelectedOrderIds.Clear();
        foreach (var orderId in orderIds)
        {
            SelectedOrderIds.Add(orderId);
        }

        if (CreatePickingTaskCommand is AsyncRelayCommand cmd)
        {
            cmd.RaiseCanExecuteChanged();
        }
    }

    private async Task RefreshOrdersAsync()
    {
        var page = await _apiClient.GetOrdersAsync(SearchText, _page, 100, CancellationToken.None);
        Orders.Clear();
        foreach (var item in page.Items)
        {
            Orders.Add(item);
        }

        StatusText = $"Loaded {Orders.Count} orders.";
    }

    private async Task RefreshStockAsync()
    {
        var stockRows = await _apiClient.GetStockAsync(CancellationToken.None);
        Stock.Clear();
        foreach (var row in stockRows)
        {
            Stock.Add(row);
        }

        StatusText = $"Loaded {Stock.Count} stock rows.";
    }

    private async Task RefreshDashboardAsync()
    {
        Dashboard = await _apiClient.GetDashboardAsync(CancellationToken.None);
        StatusText = "Dashboard updated.";
    }

    private async Task CreatePickingTaskAsync()
    {
        if (SelectedOrderIds.Count == 0)
        {
            return;
        }

        var task = await _apiClient.CreatePickingTaskAsync(SelectedOrderIds.ToArray(), CancellationToken.None);
        StatusText = $"Created picking task {task.TaskNumber}.";
        await RefreshOrdersAsync();
        await RefreshDashboardAsync();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
