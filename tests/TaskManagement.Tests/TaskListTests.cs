using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace TaskManagement.Tests;

public sealed class TaskListTests(TaskApiFixture fixture) : IClassFixture<TaskApiFixture>
{
    private const string Tasks = "/api/tasks";
    private const string DueDate = "2001-02-03";

    [Fact]
    public async Task CombinedExactFiltersReturnTwoOrderedPagesWithoutDuplicates()
    {
        using var client = fixture.CreateClient();
        const string filter = "status=inProgress&assignee=List%20Taylor&pageSize=2";
        var first = await CreateTask(client, "List Taylor", "inProgress");
        var otherStatus = await CreateTask(client, "List Taylor", "pending");
        var otherAssignee = await CreateTask(client, "list Taylor", "inProgress");
        var second = await CreateTask(client, "List Taylor", "inProgress");
        var third = await CreateTask(client, "List Taylor", "inProgress");

        var page = await ReadPage(client, filter);
        Assert.Equal([first, second], page.Items);
        Assert.Equal(second.Id, page.NextAfterId);

        var last = await ReadPage(client, $"{filter}&afterId={page.NextAfterId}");
        Assert.Equal([third], last.Items);
        Assert.Null(last.NextAfterId);
        var empty = await ReadPage(client, $"{filter}&afterId={third.Id}");
        Assert.Empty(empty.Items);
        Assert.Null(empty.NextAfterId);

        var byAssignee = await ReadPage(client, "assignee=List%20Taylor");
        Assert.Equal([first, otherStatus, second, third], byAssignee.Items);
        var byStatus = await ReadPage(client, $"status=inProgress&afterId={first.Id}");
        Assert.Equal([otherAssignee, second, third], byStatus.Items);
        var padded = await ReadPage(client, "assignee=%20List%20Taylor%20");
        Assert.Empty(padded.Items);
        Assert.Null(padded.NextAfterId);
    }

    [Fact]
    public async Task OmittedSizeDefaultsToTwenty_AndBothSizeBoundariesAreAccepted()
    {
        using var client = fixture.CreateClient();
        const string filter = "assignee=ListDefault";
        var expected = new List<TaskReply>();
        for (var i = 0; i < 21; i++)
            expected.Add(await CreateTask(client, "ListDefault", "done"));

        var page = await ReadPage(client, filter);
        Assert.Equal(expected.Take(20), page.Items);
        Assert.Equal(expected[19].Id, page.NextAfterId);
        var last = await ReadPage(client, $"{filter}&afterId={page.NextAfterId}");
        Assert.Equal([expected[20]], last.Items);
        Assert.Null(last.NextAfterId);

        var smallest = await ReadPage(client, $"{filter}&pageSize=1");
        Assert.Equal([expected[0]], smallest.Items);
        Assert.Equal(expected[0].Id, smallest.NextAfterId);
        var largest = await ReadPage(client, $"{filter}&pageSize=100");
        Assert.Equal(expected, largest.Items);
        Assert.Null(largest.NextAfterId);
    }

    [Fact]
    public async Task InvalidFiltersAndPaginationReturnBadRequestWithoutChangingTasksOrKeys()
    {
        using var client = fixture.CreateClient();
        const string filter = "assignee=ListValidation";
        var first = await CreateTask(client, "ListValidation", "pending");
        foreach (var (query, field) in new[]
        {
            ("status=InProgress", "status"), ("status=1", "status"), ("status=", "status"),
            ("status=%20pending%20", "status"), ("status=unknown", "status"),
            ("assignee=before%00after", "assignee"),
            ("pageSize=0", "pageSize"), ("pageSize=101", "pageSize"), ("pageSize=NaN", "pageSize"),
            ("pageSize=", "pageSize"), ("afterId=0", "afterId"), ("afterId=-1", "afterId"),
            ("afterId=2147483648", "afterId"), ("afterId=NaN", "afterId"), ("afterId=", "afterId")
        })
        {
            using var response = await client.GetAsync($"{Tasks}?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
            Assert.Equal(field, error.Name, ignoreCase: true);
            Assert.NotEmpty(error.Value.EnumerateArray());
        }

        var unchanged = await ReadPage(client, filter);
        Assert.Equal([first], unchanged.Items);
        Assert.Null(unchanged.NextAfterId);
        var second = await CreateTask(client, "ListValidation", "pending");
        Assert.Equal(first.Id + 1, second.Id);
    }

    [Theory]
    [InlineData("status=pending&status=done", "status")]
    [InlineData("assignee=Alex&assignee=Morgan", "assignee")]
    [InlineData("pageSize=1&pageSize=100", "pageSize")]
    [InlineData("afterId=1&afterId=3", "afterId")]
    [InlineData("pageSize=1%00", "pageSize")]
    [InlineData("pageSize=1%000", "pageSize")]
    [InlineData("afterId=1%00", "afterId")]
    [InlineData("afterId=1%000", "afterId")]
    public async Task RepeatedValuesAndNumericNulReturnFieldErrorsWithoutChangingTasks(string query, string field)
    {
        using var client = fixture.CreateClient();
        var before = await ReadPage(client, "pageSize=100");
        Assert.NotEmpty(before.Items);

        using var response = await client.GetAsync($"{Tasks}?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        var error = Assert.Single(problem.GetProperty("errors").EnumerateObject());
        Assert.Equal(field, error.Name, ignoreCase: true);
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(error.Value.EnumerateArray()).GetString()));

        var after = await ReadPage(client, "pageSize=100");
        Assert.Equal(before.Items, after.Items);
        Assert.Equal(before.NextAfterId, after.NextAfterId);
    }

    [Fact]
    public async Task UnfilteredListIncludesDemoTasksInIdOrder()
    {
        using var client = fixture.CreateClient();
        var page = await ReadPage(client, "afterId=1");
        var seed = page.Items.Take(2).ToArray();
        Assert.Equal([2, 3], seed.Select(task => task.Id));
        Assert.Equal(["inProgress", "done"], seed.Select(task => task.Status));
    }

    private static async Task<TaskReply> CreateTask(HttpClient client, string assignee, string status)
    {
        const string title = "List task";
        const string description = "List fixture row";
        using var created = await client.PostAsJsonAsync(Tasks, new { title, description, assignee, status, dueDate = DueDate });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var json = await created.Content.ReadFromJsonAsync<JsonElement>();
        return new TaskReply(json.GetProperty("id").GetInt32(), title, description, status, assignee, DueDate);
    }

    private static async Task<PageReply> ReadPage(HttpClient client, string query)
    {
        using var response = await client.GetAsync($"{Tasks}?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(new[] { "items", "nextAfterId" }, json.EnumerateObject().Select(field => field.Name).Order());
        var items = json.GetProperty("items").EnumerateArray().Select(ReadTask).ToArray();
        var cursor = json.GetProperty("nextAfterId");
        return new PageReply(items, cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetInt32());
    }

    private static TaskReply ReadTask(JsonElement json)
    {
        Assert.Equal(new[] { "assignee", "description", "dueDate", "id", "status", "title" },
            json.EnumerateObject().Select(field => field.Name).Order());
        return new TaskReply(json.GetProperty("id").GetInt32(), json.GetProperty("title").GetString(),
            json.GetProperty("description").GetString(), json.GetProperty("status").GetString(),
            json.GetProperty("assignee").GetString(), json.GetProperty("dueDate").GetString());
    }

    private sealed record PageReply(TaskReply[] Items, int? NextAfterId);
    private sealed record TaskReply(int Id, string? Title, string? Description, string? Status, string? Assignee,
        string? DueDate);
}
