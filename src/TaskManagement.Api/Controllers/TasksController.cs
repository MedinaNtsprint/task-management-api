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

    [HttpPost]
    public async Task<ActionResult<TaskResponse>> CreateAsync(CreateTaskRequest input,
        CancellationToken cancellationToken)
    {
        var created = await tasks.CreateAsync(input, cancellationToken);
        return CreatedAtRoute(GetByIdRoute, new { id = created.Id }, created);
    }

    [HttpGet]
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
    public async Task<ActionResult<TaskResponse>> GetByIdAsync([FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var result = await tasks.GetByIdAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaskResponse>> UpdateAsync([FromRoute, Range(1, int.MaxValue)] int id,
        UpdateTaskRequest input, CancellationToken cancellationToken)
    {
        var result = await tasks.UpdateAsync(id, input, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync([FromRoute, Range(1, int.MaxValue)] int id,
        CancellationToken cancellationToken)
    {
        var result = await tasks.DeleteAsync(id, cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound();
    }
}
