using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace TaskManagement.Tests;

public sealed class TaskOpenApiTests(TaskApiFixture fixture) : IClassFixture<TaskApiFixture>
{
    private const string OpenApiRoute = "/openapi/v1.json";
    private const string ScalarRoute = "/scalar/v1";

    [Fact]
    public async Task OpenApiDescribesAllFiveTaskOperationsInProduction()
    {
        await using var production = fixture.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync(OpenApiRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var paths = root.GetProperty("paths");
        Assert.Equal(["/api/tasks", "/api/tasks/{id}"], paths.EnumerateObject().Select(path => path.Name).Order());
        var collection = paths.GetProperty("/api/tasks");
        var item = paths.GetProperty("/api/tasks/{id}");
        Assert.Equal(["get", "post"], collection.EnumerateObject().Select(operation => operation.Name).Order());
        Assert.Equal(["delete", "get", "put"], item.EnumerateObject().Select(operation => operation.Name).Order());

        var list = collection.GetProperty("get");
        var create = collection.GetProperty("post");
        var get = item.GetProperty("get");
        var update = item.GetProperty("put");
        var delete = item.GetProperty("delete");
        AssertOperation(list, "List tasks", "200", "400", "429", "500");
        AssertOperation(create, "Create a task", "201", "400", "429", "500");
        AssertOperation(get, "Get a task", "200", "400", "404", "429", "500");
        AssertOperation(update, "Replace a task", "200", "400", "404", "429", "500");
        AssertOperation(delete, "Delete a task", "204", "400", "404", "429", "500");

        var listDescription = list.GetProperty("description").GetString();
        Assert.Contains("case-sensitive", listDescription, StringComparison.Ordinal);
        Assert.Contains("nextAfterId", listDescription, StringComparison.Ordinal);
        Assert.Contains("Location", create.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("YYYY-MM-DD", create.GetProperty("description").GetString(), StringComparison.Ordinal);
        Assert.Contains("YYYY-MM-DD", update.GetProperty("description").GetString(), StringComparison.Ordinal);
        AssertListParameters(list);

        var page = ResolveSchema(root, GetResponse(list, "200"), "application/json");
        AssertProperties(page, "items", "nextAfterId");
        var tasks = page.GetProperty("properties").GetProperty("items");
        Assert.Equal("array", tasks.GetProperty("type").GetString());
        AssertTaskSchema(ResolveReference(root, tasks.GetProperty("items")), includesId: true);
        AssertTaskSchema(ResolveSchema(root, GetResponse(create, "201"), "application/json"), includesId: true);
        AssertTaskSchema(ResolveSchema(root, GetResponse(get, "200"), "application/json"), includesId: true);
        AssertTaskSchema(ResolveSchema(root, GetResponse(update, "200"), "application/json"), includesId: true);
        Assert.False(GetResponse(delete, "204").TryGetProperty("content", out _));
        foreach (var operation in new[] { create, update })
        {
            var body = operation.GetProperty("requestBody");
            Assert.True(body.GetProperty("required").GetBoolean());
            var schema = ResolveSchema(root, body, "application/json");
            AssertTaskSchema(schema, includesId: false);
            Assert.Equal(5, schema.GetProperty("required").GetArrayLength());
        }

        foreach (var operation in new[] { list, create, get, update, delete })
        {
            var validation = ResolveSchema(root, GetResponse(operation, "400"), "application/problem+json");
            Assert.True(validation.GetProperty("properties").TryGetProperty("errors", out _));
            foreach (var status in new[] { "404", "429", "500" })
            {
                if (!operation.GetProperty("responses").TryGetProperty(status, out var error))
                    continue;

                var problem = ResolveSchema(root, error, "application/problem+json");
                Assert.True(problem.GetProperty("properties").TryGetProperty("status", out _));
                Assert.True(problem.GetProperty("properties").TryGetProperty("title", out _));
            }
        }
    }

    [Fact]
    public async Task ScalarServesProductionHtmlAndBundledScriptWithoutDefaultCdnFonts()
    {
        await using var production = fixture.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        using var client = production.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await client.GetAsync(ScalarRoute);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Task Management API</title>", html, StringComparison.Ordinal);
        // The third argument to Scalar's initializer is the serialized configuration.
        var initialize = Regex.Match(html, """\binitialize\(\s*'[^']*',\s*(?:true|false),\s*(\{[^\r\n]+\}),""");
        Assert.True(initialize.Success, "The Scalar HTML did not embed the viewer configuration.");
        using var configuration = JsonDocument.Parse(initialize.Groups[1].Value);
        var settings = configuration.RootElement;
        Assert.False(settings.GetProperty("withDefaultFonts").GetBoolean());
        Assert.False(settings.GetProperty("telemetry").GetBoolean());
        var source = Assert.Single(settings.GetProperty("sources").EnumerateArray());
        Assert.Equal(OpenApiRoute.TrimStart('/'), source.GetProperty("url").GetString());
        Assert.DoesNotContain("fonts.scalar.com", html, StringComparison.OrdinalIgnoreCase);

        var scripts = Regex.Matches(html, """<script\b[^>]*\bsrc=["']([^"']+)["']""")
            .Select(match => match.Groups[1].Value).ToArray();
        var pageUri = new Uri(client.BaseAddress!, ScalarRoute);
        var scriptUris = scripts.Select(src => new Uri(pageUri, src)).ToArray();
        Assert.Contains(scriptUris, uri => uri.AbsolutePath == "/scalar/scalar.js");
        Assert.Contains(scriptUris, uri => uri.AbsolutePath == "/scalar/scalar.aspnetcore.js");
        foreach (var scriptUri in scriptUris)
        {
            Assert.Equal(pageUri.GetLeftPart(UriPartial.Authority), scriptUri.GetLeftPart(UriPartial.Authority));
            using var asset = await client.GetAsync(scriptUri);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal("text/javascript", asset.Content.Headers.ContentType?.MediaType);
            Assert.NotEmpty(await asset.Content.ReadAsByteArrayAsync());
        }
    }

    [Theory]
    [InlineData(OpenApiRoute)]
    [InlineData(ScalarRoute)]
    public async Task DocumentationDoesNotBypassHostAllowlist(string path)
    {
        using var client = fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Host = "example.test";
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static void AssertOperation(JsonElement operation, string summary, params string[] statuses)
    {
        Assert.Equal(summary, operation.GetProperty("summary").GetString());
        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("description").GetString()));
        Assert.Equal(statuses, operation.GetProperty("responses").EnumerateObject().Select(status => status.Name).Order());
    }

    private static void AssertListParameters(JsonElement list)
    {
        var parameters = list.GetProperty("parameters").EnumerateArray()
            .ToDictionary(parameter => parameter.GetProperty("name").GetString()!, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(["afterid", "assignee", "pagesize", "status"], parameters.Keys.Select(key => key.ToLowerInvariant()).Order());
        foreach (var parameter in parameters.Values)
        {
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.False(parameter.TryGetProperty("required", out var required) && required.GetBoolean());
        }
    }

    private static void AssertTaskSchema(JsonElement schema, bool includesId)
    {
        string[] names = includesId
            ? ["assignee", "description", "dueDate", "id", "status", "title"]
            : ["assignee", "description", "dueDate", "status", "title"];
        AssertProperties(schema, names);
        Assert.Equal("date", schema.GetProperty("properties").GetProperty("dueDate").GetProperty("format").GetString());
    }

    private static void AssertProperties(JsonElement schema, params string[] names) =>
        Assert.Equal(names, schema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order());

    private static JsonElement GetResponse(JsonElement operation, string status) =>
        operation.GetProperty("responses").GetProperty(status);

    private static JsonElement ResolveSchema(JsonElement root, JsonElement contentOwner, string mediaType) =>
        ResolveReference(root, contentOwner.GetProperty("content").GetProperty(mediaType).GetProperty("schema"));

    private static JsonElement ResolveReference(JsonElement root, JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var reference))
            return schema;

        const string prefix = "#/components/schemas/";
        var pointer = reference.GetString()!;
        Assert.StartsWith(prefix, pointer, StringComparison.Ordinal);
        return root.GetProperty("components").GetProperty("schemas").GetProperty(pointer[prefix.Length..]);
    }
}
