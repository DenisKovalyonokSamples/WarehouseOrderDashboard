using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Warehouse.Wpf.Models;

namespace Warehouse.Wpf.Services;

/// <summary>
/// HTTP-based implementation of <see cref="IWarehouseApiClient"/>.
/// </summary>
public sealed class WarehouseApiClient(HttpClient httpClient) : IWarehouseApiClient
{
    private readonly HttpClient _httpClient = httpClient;

    /// <inheritdoc />
    public async Task<PagedResult<OrderListItemDto>> GetOrdersAsync(string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var requestUri = $"api/orders?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            requestUri += $"&search={Uri.EscapeDataString(search)}";
        }

        return await _httpClient.GetFromJsonAsync<PagedResult<OrderListItemDto>>(requestUri, cancellationToken)
            ?? new PagedResult<OrderListItemDto>();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<StockOverviewDto>> GetStockAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<IReadOnlyCollection<StockOverviewDto>>("api/stock", cancellationToken)
            ?? Array.Empty<StockOverviewDto>();
    }

    /// <inheritdoc />
    public async Task<DashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        return await _httpClient.GetFromJsonAsync<DashboardDto>("api/dashboard/today", cancellationToken)
            ?? new DashboardDto();
    }

    /// <inheritdoc />
    public async Task<PickingTaskDto> CreatePickingTaskAsync(IReadOnlyCollection<int> orderIds, CancellationToken cancellationToken)
    {
        var httpResponse = await _httpClient.PostAsJsonAsync("api/picking-tasks", new { orderIds }, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var content = await httpResponse.Content.ReadAsStringAsync(cancellationToken);

            try
            {
                var problem = JsonSerializer.Deserialize<ApiProblemDetails>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (!string.IsNullOrWhiteSpace(problem?.Title))
                {
                    throw new InvalidOperationException(problem.Title);
                }
            }
            catch (JsonException)
            {
            }

            throw new HttpRequestException($"Create picking task failed with status code {(int)httpResponse.StatusCode}.");
        }

        return await httpResponse.Content.ReadFromJsonAsync<PickingTaskDto>(cancellationToken: cancellationToken)
            ?? new PickingTaskDto();
    }

    private sealed class ApiProblemDetails
    {
        public string? Title { get; set; }
    }
}
