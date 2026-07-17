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

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            error = exception?.Message,
            inner = exception?.InnerException?.Message,
            stack = exception?.StackTrace
        });
    });
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Customers"" (
            ""Id"" uuid NOT NULL,
            ""Name"" character varying(200) NOT NULL,
            ""Email"" character varying(200) NOT NULL,
            ""Address"" character varying(500) NOT NULL,
            CONSTRAINT ""PK_Customers"" PRIMARY KEY (""Id"")
        );
    ");
}

app.Run();
