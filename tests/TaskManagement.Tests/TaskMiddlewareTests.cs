using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskManagement.Api.Services;
using Xunit;

namespace TaskManagement.Tests;

public sealed class TaskMiddlewareTests(TaskApiFixture fixture) : IClassFixture<TaskApiFixture>
{
    private const string MissingRoute = "/not-an-api-route";
    private const string FailureMessage = "T6 test-only backend failure";

    [Fact]
    public async Task TaskEndpointsShareTwoPermits_AndRejectedDeleteDoesNotReachTheDatabase()
    {
        await using var limited = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:RequestsPerMinute", "2"));
        using var firstClient = limited.CreateClient();
        using var secondClient = limited.CreateClient();
        using var observer = fixture.CreateClient();
        const string item = "/api/tasks/2";

        using var outsidePolicy = await firstClient.GetAsync(MissingRoute);
        Assert.Equal(HttpStatusCode.NotFound, outsidePolicy.StatusCode);
        using var first = await firstClient.GetAsync(item);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await first.Content.ReadAsStringAsync();
        using var second = await secondClient.GetAsync("/api/tasks");
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var rejected = await secondClient.DeleteAsync(item);
        await AssertProblem(rejected, HttpStatusCode.TooManyRequests, "Too Many Requests");
        Assert.NotNull(rejected.Headers.RetryAfter);
        Assert.NotNull(rejected.Headers.RetryAfter.Delta);
        Assert.InRange(rejected.Headers.RetryAfter.Delta.Value, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1));

        using var stillOutsidePolicy = await secondClient.GetAsync(MissingRoute);
        Assert.Equal(HttpStatusCode.NotFound, stillOutsidePolicy.StatusCode);
        using var unchanged = await observer.GetAsync(item);
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal(original, await unchanged.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ServiceExceptionReturnsGenericProblemWithoutExceptionDetails()
    {
        await using var faulting = fixture.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITaskService>();
                services.AddScoped<ITaskService>(_ => ThrowTestServiceException());
            });
        });
        using var client = faulting.CreateClient();
        using var response = await client.GetAsync("/api/tasks");

        var problem = await AssertProblem(response, HttpStatusCode.InternalServerError,
            "An error occurred while processing your request.");
        var body = problem.GetRawText();
        Assert.DoesNotContain(FailureMessage, body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ThrowTestServiceException), body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.False(problem.TryGetProperty("detail", out _));
    }

    [Fact]
    public async Task UnmatchedRouteReturnsNotFoundProblem()
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync(MissingRoute);

        await AssertProblem(response, HttpStatusCode.NotFound, "Not Found");
    }

    private static ITaskService ThrowTestServiceException() => throw new InvalidOperationException(FailureMessage);

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status,
        string title)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(title, problem.RootElement.GetProperty("title").GetString());
        return problem.RootElement.Clone();
    }
}
