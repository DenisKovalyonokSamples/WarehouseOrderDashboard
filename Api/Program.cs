using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Warehouse.Application.Services;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Warehouse API",
        Version = "v1",
        Description = "HTTP API for warehouse orders, stock, dashboard, and picking workflows."
    });

    var xmlDocumentationAssemblies = new[]
    {
        Assembly.GetExecutingAssembly().GetName().Name,
        typeof(IOrderWorkflowService).Assembly.GetName().Name,
        typeof(Warehouse.Domain.OrderStatus).Assembly.GetName().Name
    };

    foreach (var assemblyName in xmlDocumentationAssemblies.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct())
    {
        var xmlFileName = $"{assemblyName}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFileName);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
});

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
