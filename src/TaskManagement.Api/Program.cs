using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

var app = builder.Build();
app.MapControllers();
await app.RunAsync();

public partial class Program { }
