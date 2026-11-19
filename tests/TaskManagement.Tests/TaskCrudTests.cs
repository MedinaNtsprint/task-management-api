using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace TaskManagement.Tests;

public sealed class TaskCrudTests(TaskApiFixture fixture) : IClassFixture<TaskApiFixture>
{
    private const string Tasks = "/api/tasks";
    private const string PastDate = "2001-02-03";
    private static readonly string[] WritableTextFields = ["title", "description", "assignee"];

    [Theory]
    [InlineData("localhost:8080", HttpStatusCode.OK)]
    [InlineData("127.0.0.1:8080", HttpStatusCode.OK)]
    [InlineData("untrusted.example:8080", HttpStatusCode.BadRequest)]
    public async Task OnlyLocalHostHeadersCanReachTheApi(string host, HttpStatusCode status)
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Tasks}/2");
        request.Headers.Host = host;
        using var response = await client.SendAsync(request);
        Assert.Equal(status, response.StatusCode);
        if (status == HttpStatusCode.OK)
            Assert.Equal(2, (await ReadTask(response)).Id);
    }

    [Fact]
    public async Task InvalidCreatesDoNotConsumeTheFirstKey_AndCreateLocationReturnsTheStoredTask()
    {
        using var client = fixture.CreateClient();
        foreach (var (field, value) in new (string, JsonNode?)[]
        {
            ("title", " \t "), ("title", new string('t', 121)),
            ("description", " "), ("description", new string('d', 2001)),
            ("assignee", "\n"), ("assignee", new string('a', 101)),
            ("status", "Pending"), ("status", "inprogress"), ("status", "1"),
            ("status", 1), ("dueDate", "2001-02-30"), ("dueDate", "02/03/2001")
        })
        {
            var input = Input();
            input[field] = value;
            using var rejected = await client.PostAsJsonAsync(Tasks, input);
            await AssertValidationError(rejected, field);
            Assert.Null(rejected.Headers.Location);
        }
        foreach (var field in new[] { "title", "description", "assignee", "status", "dueDate" })
        {
            var input = Input();
            input.Remove(field);
            using var rejected = await client.PostAsJsonAsync(Tasks, input);
            await AssertValidationError(rejected, field);
            Assert.Null(rejected.Headers.Location);
        }
        using var brokenJson = new StringContent("{", Encoding.UTF8, "application/json");
        using var malformed = await client.PostAsync(Tasks, brokenJson);
        await AssertValidationError(malformed, "$");
        Assert.Null(malformed.Headers.Location);

        foreach (var field in WritableTextFields)
        {
            var input = Input();
            input[field] = "before\0after";
            using var rejected = await client.PostAsJsonAsync(Tasks, input);
            await AssertValidationError(rejected, field);
            Assert.Null(rejected.Headers.Location);
        }

        using var created = await client.PostAsJsonAsync(Tasks, Input());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var task = await ReadTask(created);
        // The three seeded IDs precede the first generated ID in this fixture.
        Assert.Equal(new TaskReply(4, "New task 📝", "New\ndescription", "pending", "Taylór", PastDate), task);
        Assert.NotNull(created.Headers.Location);
        Assert.Equal($"{Tasks}/{task.Id}", created.Headers.Location.AbsolutePath);
        using var read = await client.GetAsync(created.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(task, await ReadTask(read));
    }

    [Fact]
    public async Task PutReplacesEveryField_AndInvalidReplacementLeavesTheTaskUnchanged()
    {
        using var client = fixture.CreateClient();
        var path = $"{Tasks}/1";
        using var original = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        var before = await ReadTask(original);
        Assert.Equal("pending", before.Status);

        var input = Input();
        input["status"] = "inProgress";
        var full = (JsonObject)input.DeepClone();
        input.Remove("description");
        using var rejected = await client.PutAsJsonAsync(path, input);
        await AssertValidationError(rejected, "description");
        foreach (var field in WritableTextFields)
        {
            var withNul = (JsonObject)full.DeepClone();
            withNul[field] = "before\0after";
            using var rejectedNul = await client.PutAsJsonAsync(path, withNul);
            await AssertValidationError(rejectedNul, field);
        }
        using var afterRejected = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, afterRejected.StatusCode);
        Assert.Equal(before, await ReadTask(afterRejected));

        var expected = new TaskReply(1, new string('t', 120), new string('d', 2000),
            "inProgress", new string('a', 100), PastDate);
        full["title"] = expected.Title;
        full["description"] = expected.Description;
        full["assignee"] = expected.Assignee;
        using var updated = await client.PutAsJsonAsync(path, full);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(expected, await ReadTask(updated));
        using var read = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(expected, await ReadTask(read));
    }

    [Fact]
    public async Task DeleteReturnsNoContent_ThenReadAndRepeatDeleteReturnNotFound()
    {
        using var client = fixture.CreateClient();
        var path = $"{Tasks}/3";
        using var original = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
        Assert.Equal("done", (await ReadTask(original)).Status);
        using var deleted = await client.DeleteAsync(path);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsByteArrayAsync());
        using var read = await client.GetAsync(path);
        await AssertProblem(read, HttpStatusCode.NotFound);
        using var repeated = await client.DeleteAsync(path);
        await AssertProblem(repeated, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("not-an-id", HttpStatusCode.BadRequest)]
    [InlineData("2147483648", HttpStatusCode.BadRequest)]
    [InlineData("0", HttpStatusCode.BadRequest)]
    [InlineData("-1", HttpStatusCode.BadRequest)]
    [InlineData("2147483647", HttpStatusCode.NotFound)]
    public async Task ItemMethodsDistinguishInvalidIdsFromMissingTasks(string id, HttpStatusCode status)
    {
        using var client = fixture.CreateClient();
        var path = $"{Tasks}/{id}";
        using var read = await client.GetAsync(path);
        using var update = await client.PutAsJsonAsync(path, Input());
        using var delete = await client.DeleteAsync(path);
        foreach (var response in new[] { read, update, delete })
        {
            if (status == HttpStatusCode.BadRequest)
                await AssertValidationError(response, "id");
            else
                await AssertProblem(response, status);
        }
    }

    private static JsonObject Input() => new()
    {
        ["title"] = "  New task 📝 ", ["description"] = " New\ndescription\t",
        ["status"] = "pending", ["assignee"] = " Taylór ", ["dueDate"] = PastDate
    };

    private static async Task AssertValidationError(HttpResponseMessage response, string field)
    {
        var problem = await AssertProblem(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors");
        Assert.True(errors.TryGetProperty(field, out var messages) ||
            errors.TryGetProperty($"$.{field}", out messages), $"Expected a validation error for {field}: {errors}");
        Assert.NotEmpty(messages.EnumerateArray());
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        return problem;
    }

    private static async Task<TaskReply> ReadTask(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new TaskReply(json.GetProperty("id").GetInt32(), json.GetProperty("title").GetString(),
            json.GetProperty("description").GetString(), json.GetProperty("status").GetString(),
            json.GetProperty("assignee").GetString(), json.GetProperty("dueDate").GetString());
    }

    private sealed record TaskReply(int Id, string? Title, string? Description, string? Status, string? Assignee,
        string? DueDate);
}
