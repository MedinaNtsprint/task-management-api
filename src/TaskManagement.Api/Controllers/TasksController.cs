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
