using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TaskManagement.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace TaskManagement.Tests;

public sealed class TaskApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder(
            "postgres@sha256:eee6c6e1b0894fa4fe0075eb054af9fd1cf39a35321c2d4110626cfe506411fb")
        .WithDatabase("tasks_test")
        .WithUsername($"test_{Guid.NewGuid():N}")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .WithCreateParameterModifier(parameters =>
        {
            var portBindings = parameters.HostConfig?.PortBindings;
            if (portBindings is null || portBindings.Count == 0)
                throw new InvalidOperationException("A loopback binding is required for the test database.");

            foreach (var binding in portBindings.Values.SelectMany(bindings => bindings))
                binding.HostIP = "127.0.0.1";
        })
        .Build();
    private WebApplicationFactory<Program>? factory;

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await postgres.StartAsync(timeout.Token);
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<NpgsqlDataSource>();
                services.AddSingleton(_ => NpgsqlDataSource.Create(postgres.GetConnectionString()));
            }));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TasksDbContext>().Database.MigrateAsync(timeout.Token);
    }

    public HttpClient CreateClient() => factory!.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });

    public WebApplicationFactory<Program> WithWebHostBuilder(Action<IWebHostBuilder> configure) =>
        factory!.WithWebHostBuilder(configure);

    public async Task DisposeAsync()
    {
        try
        {
            if (factory is not null)
                await factory.DisposeAsync();
        }
        finally
        {
            await postgres.DisposeAsync();
        }
    }
}
