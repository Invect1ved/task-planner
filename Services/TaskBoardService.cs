using Microsoft.EntityFrameworkCore;
using TaskPlanner.Data;
using TaskPlanner.Data.Entities;

namespace TaskPlanner.Services;

public record TaskDto(
    int Id,
    string Title,
    string? Description,
    DateTime DueAt,
    DateTime CreatedAt,
    bool IsCompleted,
    bool IsOwner,
    string OwnerName,
    string OwnerEmail,
    IReadOnlyList<string> SharedWith);

public class TaskBoardService
{
    private readonly PlannerDbContext db;

    public TaskBoardService(PlannerDbContext db)
    {
        this.db = db;
    }

    public async Task<IReadOnlyList<TaskDto>> ListAsync(int userId, string sort, string direction, string filter)
    {
        var query = Visible(userId);
        query = filter switch
        {
            "active" => query.Where(t => !t.IsCompleted),
            "done" => query.Where(t => t.IsCompleted),
            "shared" => query.Where(t => t.OwnerId != userId),
            _ => query
        };

        var descending = direction == "desc";
        query = sort == "due"
            ? (descending ? query.OrderByDescending(t => t.DueAt) : query.OrderBy(t => t.DueAt))
            : (descending ? query.OrderByDescending(t => t.CreatedAt) : query.OrderBy(t => t.CreatedAt));

        var items = await query.ToListAsync();
        return items.Select(t => Map(t, userId)).ToList();
    }

    public async Task<IReadOnlyList<TaskDto>> UpcomingAsync(int userId)
    {
        var until = DateTime.Now.AddHours(2);
        var items = await Visible(userId)
            .Where(t => !t.IsCompleted && t.DueAt <= until)
            .OrderBy(t => t.DueAt)
            .ToListAsync();
        return items.Select(t => Map(t, userId)).ToList();
    }

    public async Task<(bool Ok, string? Error, TaskDto? Task)> CreateAsync(int userId, string title, string? description, DateTime dueAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return (false, "Укажите название.", null);
        }

        var task = new PlannerTask
        {
            OwnerId = userId,
            Title = title.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            DueAt = dueAt,
            CreatedAt = DateTime.Now
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();
        var stored = await Visible(userId).SingleAsync(t => t.Id == task.Id);
        return (true, null, Map(stored, userId));
    }

    public async Task<(bool Ok, string? Error, TaskDto? Task)> UpdateAsync(int userId, int taskId, string title, string? description, DateTime dueAt, bool isCompleted)
    {
        var task = await db.Tasks.Include(t => t.Shares).SingleOrDefaultAsync(t => t.Id == taskId);
        if (task == null || !CanSee(task, userId))
        {
            return (false, "Задача не найдена.", null);
        }

        if (task.OwnerId != userId)
        {
            task.IsCompleted = isCompleted;
            task.CompletedAt = isCompleted ? DateTime.Now : null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, "Укажите название.", null);
            }

            task.Title = title.Trim();
            task.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            task.DueAt = dueAt;
            task.IsCompleted = isCompleted;
            task.CompletedAt = isCompleted ? task.CompletedAt ?? DateTime.Now : null;
        }

        await db.SaveChangesAsync();
        var stored = await Visible(userId).SingleAsync(t => t.Id == task.Id);
        return (true, null, Map(stored, userId));
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int userId, int taskId)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == taskId);
        if (task == null || task.OwnerId != userId)
        {
            return (false, "Удалить задачу может только её автор.");
        }

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Ok, string? Error)> ShareAsync(int userId, int taskId, string email)
    {
        var task = await db.Tasks.SingleOrDefaultAsync(t => t.Id == taskId);
        if (task == null || task.OwnerId != userId)
        {
            return (false, "Делиться задачей может только её автор.");
        }

        var other = await db.Users.SingleOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant());
        if (other == null)
        {
            return (false, "Пользователь с такой почтой не найден.");
        }

        if (other.Id == userId)
        {
            return (false, "Нельзя открыть задачу самому себе.");
        }

        var exists = await db.Shares.AnyAsync(s => s.TaskId == taskId && s.UserId == other.Id);
        if (!exists)
        {
            db.Shares.Add(new TaskShare { TaskId = taskId, UserId = other.Id });
            await db.SaveChangesAsync();
        }

        return (true, null);
    }

    private IQueryable<PlannerTask> Visible(int userId) =>
        db.Tasks
            .Include(t => t.Owner)
            .Include(t => t.Shares).ThenInclude(s => s.User)
            .Where(t => t.OwnerId == userId || t.Shares.Any(s => s.UserId == userId));

    private static bool CanSee(PlannerTask task, int userId) =>
        task.OwnerId == userId || task.Shares.Any(s => s.UserId == userId);

    private static TaskDto Map(PlannerTask task, int userId) => new(
        task.Id,
        task.Title,
        task.Description,
        task.DueAt,
        task.CreatedAt,
        task.IsCompleted,
        task.OwnerId == userId,
        task.Owner.DisplayName,
        task.Owner.Email,
        task.Shares.Select(s => s.User.Email).ToList());
}
