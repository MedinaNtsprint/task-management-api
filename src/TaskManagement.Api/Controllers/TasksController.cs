using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Contracts;
using TaskManagement.Api.Services;

namespace TaskManagement.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TasksController(ITaskService tasks) : ControllerBase
{
    private const string GetByIdRoute = "GetTaskById";
    private const string ProblemJson = "application/problem+json";

    [HttpPost]
    [EndpointSummary("Create a task")]
    [EndpointDescription("Supply all five task fields. Status must be pending, inProgress, or done. " +
        "The due date is a calendar date (YYYY-MM-DD); past dates are allowed. " +
        "Returns the created task and a Location header pointing to its item URL.")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<ActionResult<TaskResponse>> CreateAsync(CreateTaskRequest input,
        CancellationToken cancellationToken)
    {
        var created = await tasks.CreateAsync(input, cancellationToken);
        return CreatedAtRoute(GetByIdRoute, new { id = created.Id }, created);
    }

    [HttpGet]
    [EndpointSummary("List tasks")]
    [EndpointDescription("Combine optional status (pending, inProgress, or done) and assignee filters. " +
        "Both are exact, case-sensitive matches; assignee is not trimmed. Supply each query parameter at most once. " +
        "Results are sorted by ascending ID. Page size is 1 to 100 (default 20). Omit afterId on the first page; " +
        "then pass the positive nextAfterId as afterId with the same filters. A null nextAfterId means there are no more matches.")]
    [ProducesResponseType<TaskPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<ActionResult<TaskPageResponse>> ListAsync([FromQuery(Name = "")] TaskListQuery query,
        CancellationToken cancellationToken)
    {
        CheckScalar(nameof(TaskListQuery.Status));
        CheckScalar(nameof(TaskListQuery.Assignee));
        CheckScalar(nameof(TaskListQuery.PageSize), numeric: true);
        // The nullable binder treats an empty cursor as null, but only omission is valid.
        if (CheckScalar(nameof(TaskListQuery.AfterId), numeric: true) && query.AfterId is null)
            ModelState.AddModelError(nameof(TaskListQuery.AfterId), "AfterId must be a positive integer.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return Ok(await tasks.ListAsync(query, cancellationToken));

        // Returns whether a single acceptable raw value was present.
        bool CheckScalar(string field, bool numeric = false)
        {
            if (!Request.Query.TryGetValue(field, out var values))
                return false;

            if (values.Count != 1)
                ModelState.AddModelError(field, $"Supply only one value for {field}.");
            else if (numeric && values[0]?.Contains('\0') == true)
                ModelState.AddModelError(field, $"The {field} field must not contain a NUL character.");
            else
                return true;

            return false;
        }
    }

    [HttpGet("{id}", Name = GetByIdRoute)]
    [EndpointSummary("Get a task")]
    [EndpointDescription("Return the task with the specified positive integer ID. A valid ID with no matching task returns 404.")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<ActionResult<TaskResponse>> GetByIdAsync([FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var result = await tasks.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("{id}")]
    [EndpointSummary("Replace a task")]
    [EndpointDescription("Replace all five fields of the task with the specified positive integer ID. " +
        "Status must be pending, inProgress, or done. The due date is a calendar date (YYYY-MM-DD); past dates are allowed. " +
        "Returns the updated task, or 404 if no task matches the ID.")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<ActionResult<TaskResponse>> UpdateAsync([FromRoute, Range(1, int.MaxValue)] int id,
        UpdateTaskRequest input, CancellationToken cancellationToken)
    {
        var result = await tasks.UpdateAsync(id, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Delete a task")]
    [EndpointDescription("Delete the task with the specified positive integer ID. Success has no response body; " +
        "a valid ID with no matching task returns 404.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status500InternalServerError, ProblemJson)]
    public async Task<IActionResult> DeleteAsync([FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var result = await tasks.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound();
    }
}
