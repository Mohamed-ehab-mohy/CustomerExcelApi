using CustomerExcelApi.Data;
using CustomerExcelApi.Features.Customers.Commands.ImportCustomers;
using CustomerExcelApi.Features.Customers.Queries.ExportCustomers;
using CustomerExcelApi.Hubs;
using CustomerExcelApi.Interfaces;
using CustomerExcelApi.Repositories;
using CustomerExcelApi.Services;
using CustomerExcelApi.Services.Notifications;
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

builder.Services.AddSignalR();

builder.Services.AddSingleton<SignalRNotificationProvider>();
builder.Services.AddSingleton<WebPushNotificationProvider>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<ReminderBackgroundService>();

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

app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""Reminders"" (
            ""Id"" uuid NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""Title"" character varying(200) NOT NULL,
            ""Message"" character varying(1000) NOT NULL,
            ""MeetingTime"" timestamp with time zone NOT NULL,
            ""NextReminderTime"" timestamp with time zone NOT NULL,
            ""NotifyBeforeMinutes"" integer NOT NULL,
            ""RepeatEveryMinutes"" integer NOT NULL,
            ""RetryCount"" integer NOT NULL,
            ""MaxRetryCount"" integer NOT NULL,
            ""Status"" integer NOT NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL,
            ""UpdatedAt"" timestamp with time zone NOT NULL,
            ""ReadAt"" timestamp with time zone NULL,
            CONSTRAINT ""PK_Reminders"" PRIMARY KEY (""Id"")
        );

        CREATE INDEX IF NOT EXISTS ""IX_Reminders_Status_NextReminderTime""
            ON ""Reminders"" (""Status"", ""NextReminderTime"");

        CREATE INDEX IF NOT EXISTS ""IX_Reminders_UserId""
            ON ""Reminders"" (""UserId"");

        CREATE INDEX IF NOT EXISTS ""IX_Reminders_UserId_Status""
            ON ""Reminders"" (""UserId"", ""Status"");

        CREATE TABLE IF NOT EXISTS ""PushSubscriptions"" (
            ""Id"" uuid NOT NULL,
            ""UserId"" uuid NOT NULL,
            ""Endpoint"" character varying(500) NOT NULL,
            ""P256dhKey"" character varying(200) NOT NULL,
            ""AuthKey"" character varying(200) NOT NULL,
            ""CreatedAt"" timestamp with time zone NOT NULL,
            CONSTRAINT ""PK_PushSubscriptions"" PRIMARY KEY (""Id"")
        );

        CREATE INDEX IF NOT EXISTS ""IX_PushSubscriptions_UserId""
            ON ""PushSubscriptions"" (""UserId"");

        CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PushSubscriptions_UserId_Endpoint""
            ON ""PushSubscriptions"" (""UserId"", ""Endpoint"");
    ");
}

app.Run();
