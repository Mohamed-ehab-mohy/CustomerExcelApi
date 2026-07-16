using CustomerExcelApi.Data;
using CustomerExcelApi.Features.Customers.Commands.ImportCustomers;
using CustomerExcelApi.Features.Customers.Queries.ExportCustomers;
using CustomerExcelApi.Interfaces;
using CustomerExcelApi.Repositories;
using CustomerExcelApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();
builder.Services.AddScoped<ICustomerBulkRepository, CustomerBulkRepository>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<ImportCustomersHandler>();
builder.Services.AddScoped<ExportCustomersHandler>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.Run();
