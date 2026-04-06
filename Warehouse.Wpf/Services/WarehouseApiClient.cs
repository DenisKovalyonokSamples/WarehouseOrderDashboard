using System.Net.Http;
using System.Net.Http.Json;
using Warehouse.Wpf.Models;

namespace Warehouse.Wpf.Services;

public sealed class WarehouseApiClient(HttpClient httpClient) : IWarehouseApiClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = $"api/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            query += $"&search={Uri.EscapeDataString(search)}";
        }

        return await _httpClient.GetFromJsonAsync<PagedResult<OrderListItemDto>>(query, cancellationToken)
            ?? new PagedResult<OrderListItemDto>();
    }

    public async Task<IReadOnlyCollection<StockOverviewDto>> GetStockAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyCollection<StockOverviewDto>>("api/stock", cancellationToken)
            ?? Array.Empty<StockOverviewDto>();
    }

    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard/today", cancellationToken)
            ?? new DashboardDto();
    }

    public async Task<PickingTaskDto> CreatePickingTaskAsync(IReadOnlyCollection<int> orderIds, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("api/picking-tasks", new { orderIds }, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PickingTaskDto>(cancellationToken: cancellationToken)
            ?? new PickingTaskDto();
    }
}
