using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Warehouse.Wpf.Infrastructure;
using Warehouse.Wpf.Models;
using Warehouse.Wpf.Services;

namespace Warehouse.Wpf.ViewModels;

/// <summary>
/// Main screen view model for orders, stock, and dashboard operations.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private const int PageSize = 29;
    private readonly IWarehouseApiClient _apiClient;
    private int _ordersPage = 1;
    private int _stockPage = 1;
    private int _pickingTasksPage = 1;
    private int _ordersTotalCount;
    private int _stockTotalCount;
    private int _pickingTasksTotalCount;
    private string? _searchText;
    private string _statusText = "Ready";
    private DashboardDto _dashboard = new();

    /// <summary>
    /// Initializes a new view model instance.
    /// </summary>
    public MainViewModel(IWarehouseApiClient apiClient)
    {
        _apiClient = apiClient;
        RefreshOrdersCommand = new AsyncRelayCommand(RefreshOrdersAsync);
        PreviousOrdersPageCommand = new AsyncRelayCommand(PreviousOrdersPageAsync, CanGoToPreviousOrdersPage);
        NextOrdersPageCommand = new AsyncRelayCommand(NextOrdersPageAsync, CanGoToNextOrdersPage);
        RefreshStockCommand = new AsyncRelayCommand(RefreshStockAsync);
        PreviousStockPageCommand = new AsyncRelayCommand(PreviousStockPageAsync, CanGoToPreviousStockPage);
        NextStockPageCommand = new AsyncRelayCommand(NextStockPageAsync, CanGoToNextStockPage);
        RefreshPickingTasksCommand = new AsyncRelayCommand(RefreshPickingTasksAsync);
        PreviousPickingTasksPageCommand = new AsyncRelayCommand(PreviousPickingTasksPageAsync, CanGoToPreviousPickingTasksPage);
        NextPickingTasksPageCommand = new AsyncRelayCommand(NextPickingTasksPageAsync, CanGoToNextPickingTasksPage);
        RefreshDashboardCommand = new AsyncRelayCommand(RefreshDashboardAsync);
        CreatePickingTaskCommand = new AsyncRelayCommand(CreatePickingTaskAsync, () => SelectedOrderIds.Count > 0);
    }

    /// <summary>
    /// Raised when a bindable property changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<OrderListItemDto> Orders { get; } = [];
    public ObservableCollection<StockOverviewDto> Stock { get; } = [];
    public ObservableCollection<PickingTaskListItemDto> PickingTasks { get; } = [];
    public ObservableCollection<int> SelectedOrderIds { get; } = [];

    public ICommand RefreshOrdersCommand { get; }
    public ICommand PreviousOrdersPageCommand { get; }
    public ICommand NextOrdersPageCommand { get; }
    public ICommand RefreshStockCommand { get; }
    public ICommand PreviousStockPageCommand { get; }
    public ICommand NextStockPageCommand { get; }
    public ICommand RefreshPickingTasksCommand { get; }
    public ICommand PreviousPickingTasksPageCommand { get; }
    public ICommand NextPickingTasksPageCommand { get; }
    public ICommand RefreshDashboardCommand { get; }
    public ICommand CreatePickingTaskCommand { get; }

    public string OrdersPageText => BuildPageText(_ordersPage, _ordersTotalCount);
    public string StockPageText => BuildPageText(_stockPage, _stockTotalCount);
    public string PickingTasksPageText => BuildPageText(_pickingTasksPage, _pickingTasksTotalCount);

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
            _ordersPage = 1;
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

    /// <summary>
    /// Loads initial page data.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            await RefreshOrdersAsync();
            await RefreshStockAsync();
            await RefreshPickingTasksAsync();
            await RefreshDashboardAsync();
        }
        catch (HttpRequestException)
        {
            StatusText = "API unavailable. Start Warehouse.Api or update WAREHOUSE_API_BASE_URL/appsettings.json.";
        }
    }

    private async Task RefreshPickingTasksAsync()
    {
        try
        {
            var tasksPage = await _apiClient.GetPickingTasksAsync(_pickingTasksPage, PageSize, CancellationToken.None);
            _pickingTasksTotalCount = tasksPage.TotalCount;
            PickingTasks.Clear();
            foreach (var task in tasksPage.Items)
            {
                PickingTasks.Add(task);
            }

            OnPropertyChanged(nameof(PickingTasksPageText));
            UpdatePagingCommands();
            StatusText = $"Loaded {PickingTasks.Count} picking tasks (page {_pickingTasksPage}).";
        }
        catch (HttpRequestException)
        {
            StatusText = "Unable to load picking tasks. API is unavailable.";
        }
    }

    /// <summary>
    /// Updates selected order identifiers from UI selection.
    /// </summary>
    public void UpdateSelectedOrders(IEnumerable<int> orderIds)
    {
        SelectedOrderIds.Clear();
        foreach (var selectedOrderId in orderIds)
        {
            SelectedOrderIds.Add(selectedOrderId);
        }

        if (CreatePickingTaskCommand is AsyncRelayCommand createPickingTaskCommand)
        {
            createPickingTaskCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task RefreshOrdersAsync()
    {
        try
        {
            var ordersPage = await _apiClient.GetOrdersAsync(SearchText, _ordersPage, PageSize, CancellationToken.None);
            _ordersTotalCount = ordersPage.TotalCount;
            Orders.Clear();
            foreach (var order in ordersPage.Items)
            {
                Orders.Add(order);
            }

            OnPropertyChanged(nameof(OrdersPageText));
            UpdatePagingCommands();
            StatusText = $"Loaded {Orders.Count} orders (page {_ordersPage}).";
        }
        catch (HttpRequestException)
        {
            StatusText = "Unable to load orders. API is unavailable.";
        }
    }

    private async Task RefreshStockAsync()
    {
        try
        {
            var stockPage = await _apiClient.GetStockAsync(_stockPage, PageSize, CancellationToken.None);
            _stockTotalCount = stockPage.TotalCount;
            Stock.Clear();
            foreach (var row in stockPage.Items)
            {
                Stock.Add(row);
            }

            OnPropertyChanged(nameof(StockPageText));
            UpdatePagingCommands();
            StatusText = $"Loaded {Stock.Count} stock rows (page {_stockPage}).";
        }
        catch (HttpRequestException)
        {
            StatusText = "Unable to load stock. API is unavailable.";
        }
    }

    private async Task RefreshDashboardAsync()
    {
        try
        {
            Dashboard = await _apiClient.GetDashboardAsync(CancellationToken.None);
            StatusText = "Dashboard updated.";
        }
        catch (HttpRequestException)
        {
            StatusText = "Unable to load dashboard. API is unavailable.";
        }
    }

    private async Task PreviousOrdersPageAsync()
    {
        if (_ordersPage <= 1)
        {
            return;
        }

        _ordersPage--;
        await RefreshOrdersAsync();
    }

    private async Task NextOrdersPageAsync()
    {
        if (!CanGoToNextOrdersPage())
        {
            return;
        }

        _ordersPage++;
        await RefreshOrdersAsync();
    }

    private async Task PreviousStockPageAsync()
    {
        if (_stockPage <= 1)
        {
            return;
        }

        _stockPage--;
        await RefreshStockAsync();
    }

    private async Task NextStockPageAsync()
    {
        if (!CanGoToNextStockPage())
        {
            return;
        }

        _stockPage++;
        await RefreshStockAsync();
    }

    private async Task PreviousPickingTasksPageAsync()
    {
        if (_pickingTasksPage <= 1)
        {
            return;
        }

        _pickingTasksPage--;
        await RefreshPickingTasksAsync();
    }

    private async Task NextPickingTasksPageAsync()
    {
        if (!CanGoToNextPickingTasksPage())
        {
            return;
        }

        _pickingTasksPage++;
        await RefreshPickingTasksAsync();
    }

    private bool CanGoToPreviousOrdersPage() => _ordersPage > 1;
    private bool CanGoToNextOrdersPage() => _ordersPage * PageSize < _ordersTotalCount;
    private bool CanGoToPreviousStockPage() => _stockPage > 1;
    private bool CanGoToNextStockPage() => _stockPage * PageSize < _stockTotalCount;
    private bool CanGoToPreviousPickingTasksPage() => _pickingTasksPage > 1;
    private bool CanGoToNextPickingTasksPage() => _pickingTasksPage * PageSize < _pickingTasksTotalCount;

    private void UpdatePagingCommands()
    {
        (PreviousOrdersPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextOrdersPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PreviousStockPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextStockPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PreviousPickingTasksPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextPickingTasksPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    private static string BuildPageText(int page, int totalCount)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        return $"Page {page} of {totalPages}";
    }

    private async Task CreatePickingTaskAsync()
    {
        if (SelectedOrderIds.Count == 0)
        {
            return;
        }

        try
        {
            var createdPickingTask = await _apiClient.CreatePickingTaskAsync(SelectedOrderIds.ToArray(), CancellationToken.None);
            StatusText = $"Created picking task {createdPickingTask.TaskNumber}.";
            await RefreshOrdersAsync();
            await RefreshPickingTasksAsync();
            await RefreshDashboardAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusText = ex.Message;
        }
        catch (HttpRequestException)
        {
            StatusText = "Unable to create picking task. API is unavailable.";
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
