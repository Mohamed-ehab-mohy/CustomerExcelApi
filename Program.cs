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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition");
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ICustomerReadRepository, CustomerReadRepository>();
builder.Services.AddScoped<ICustomerBulkRepository, CustomerBulkRepository>();
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<ImportCustomersHandler>();
builder.Services.AddScoped<ExportCustomersHandler>();

var app = builder.Build();

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
            inner = exception?.InnerException?.Message
        });
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowAll");

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw(@"
        DROP TABLE IF EXISTS ""Orders"" CASCADE;
        DROP TABLE IF EXISTS ""Addresses"" CASCADE;
        DROP TABLE IF EXISTS ""Customers"" CASCADE;

        CREATE TABLE ""Customers"" (
            ""Id"" uuid NOT NULL,
            ""Name"" character varying(200) NOT NULL,
            ""Email"" character varying(200) NOT NULL,
            CONSTRAINT ""PK_Customers"" PRIMARY KEY (""Id"")
        );

        CREATE TABLE ""Addresses"" (
            ""Id"" uuid NOT NULL,
            ""CustomerId"" uuid NOT NULL,
            ""Street"" character varying(300) NOT NULL,
            ""City"" character varying(100) NOT NULL,
            ""Country"" character varying(100) NOT NULL,
            CONSTRAINT ""PK_Addresses"" PRIMARY KEY (""Id""),
            CONSTRAINT ""FK_Addresses_Customers_CustomerId"" FOREIGN KEY (""CustomerId"")
                REFERENCES ""Customers"" (""Id"") ON DELETE CASCADE
        );

        CREATE TABLE ""Orders"" (
            ""Id"" uuid NOT NULL,
            ""CustomerId"" uuid NOT NULL,
            ""ProductName"" character varying(200) NOT NULL,
            ""Quantity"" integer NOT NULL,
            ""Price"" numeric(18,2) NOT NULL,
            ""OrderDate"" date NOT NULL,
            CONSTRAINT ""PK_Orders"" PRIMARY KEY (""Id""),
            CONSTRAINT ""FK_Orders_Customers_CustomerId"" FOREIGN KEY (""CustomerId"")
                REFERENCES ""Customers"" (""Id"") ON DELETE CASCADE
        );
    ");
}

app.Run();
