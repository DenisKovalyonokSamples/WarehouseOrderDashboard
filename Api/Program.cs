using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Services;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var oracleConnectionString = builder.Configuration.GetConnectionString("Oracle");
if (!string.IsNullOrWhiteSpace(oracleConnectionString))
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseOracle(oracleConnectionString));
}
else
{
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("WarehouseDb"));
}

builder.Services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
