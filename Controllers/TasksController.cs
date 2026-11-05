using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPlanner.Services;

namespace TaskPlanner.Controllers;

[Authorize]
[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly TaskBoardService board;

    public TasksController(TaskBoardService board)
    {
        this.board = board;
    }

    public record TaskBody(string Title, string? Description, DateTime DueAt, bool IsCompleted);
    public record ShareBody(string Email);

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string sort = "created", [FromQuery] string direction = "asc", [FromQuery] string filter = "all")
    {
        return Ok(await board.ListAsync(UserId(), sort, direction, filter));
    }

    [HttpGet("upcoming")]
    public async Task<IActionResult> Upcoming()
    {
        return Ok(await board.UpcomingAsync(UserId()));
    }

    [HttpPost]
    public async Task<IActionResult> Create(TaskBody body)
    {
        var result = await board.CreateAsync(UserId(), body.Title, body.Description, body.DueAt);
        return result.Ok ? Ok(result.Task) : UnprocessableEntity(new { error = result.Error });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TaskBody body)
    {
        var result = await board.UpdateAsync(UserId(), id, body.Title, body.Description, body.DueAt, body.IsCompleted);
        return result.Ok ? Ok(result.Task) : UnprocessableEntity(new { error = result.Error });
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var current = (await board.ListAsync(UserId(), "created", "asc", "all")).FirstOrDefault(t => t.Id == id);
        if (current == null)
        {
            return NotFound(new { error = "Задача не найдена." });
        }

        var result = await board.UpdateAsync(UserId(), id, current.Title, current.Description, current.DueAt, !current.IsCompleted);
        return result.Ok ? Ok(result.Task) : UnprocessableEntity(new { error = result.Error });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await board.DeleteAsync(UserId(), id);
        return result.Ok ? Ok() : UnprocessableEntity(new { error = result.Error });
    }

    [HttpPost("{id:int}/share")]
    public async Task<IActionResult> Share(int id, ShareBody body)
    {
        var result = await board.ShareAsync(UserId(), id, body.Email);
        return result.Ok ? Ok() : UnprocessableEntity(new { error = result.Error });
    }

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("nameid"));
}
