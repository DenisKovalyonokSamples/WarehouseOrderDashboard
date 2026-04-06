using Microsoft.EntityFrameworkCore;
using Warehouse.Infrastructure;
using Warehouse.Application.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IClock, SystemClock>();
builder.Services.AddScoped<Warehouse.Application.Services.IAppService, Warehouse.Application.Services.AppService>();
builder.Services.AddDbContext<Warehouse.Infrastructure.AppDbContext>(opt =>
    opt.UseInMemoryDatabase("WarehouseDb")); // replace with UseOracle(...) in real deployment

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
