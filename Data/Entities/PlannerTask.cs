using System.Text.Json.Serialization;

namespace TaskPlanner.Data.Entities;

public class PlannerTask
{
    public int Id { get; set; }
    public int OwnerId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedAt { get; set; }
    [JsonIgnore] public AppUser Owner { get; set; } = null!;
    [JsonIgnore] public ICollection<TaskShare> Shares { get; set; } = new List<TaskShare>();
}
