using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scalar.AspNetCore;
using TaskManagement.Api.Controllers;
using TaskManagement.Api.Data;
using TaskManagement.Api.Services;

var migrate = args is ["--migrate"];
var builder = WebApplication.CreateBuilder(migrate ? [] : args);
builder.Services.AddControllers(options =>
    options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()));
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddSingleton(_ => DatabaseConnection.CreateDataSource());
builder.Services.AddDbContext<TasksDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

if (migrate)
{
    await using var migrationApp = builder.Build();
    await using var scope = migrationApp.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database;
    await database.MigrateAsync();
    return;
}

const string taskRateLimit = "tasks";
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Status == StatusCodes.Status429TooManyRequests)
            context.ProblemDetails.Title ??= "Too Many Requests";
    };
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(taskRateLimit, window =>
    {
        window.PermitLimit = builder.Configuration.GetValue("RateLimiting:RequestsPerMinute", 120);
        window.Window = TimeSpan.FromMinutes(1);
        window.QueueLimit = 0;
    });
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseRateLimiter();
app.MapControllers().Add(endpoint =>
{
    if (endpoint.Metadata.OfType<ControllerActionDescriptor>().Any(action =>
            action.ControllerTypeInfo.AsType() == typeof(TasksController)))
    {
        endpoint.Metadata.Add(new EnableRateLimitingAttribute(taskRateLimit));
    }
});
app.MapOpenApi();
app.MapScalarApiReference(options => options.WithTitle("Task Management API").DisableDefaultFonts().DisableTelemetry());
await app.RunAsync();

public partial class Program { }
