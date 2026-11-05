using System.Text.Json.Serialization;

namespace TaskPlanner.Data.Entities;

public class TaskShare
{
    public int TaskId { get; set; }
    public int UserId { get; set; }
    [JsonIgnore] public PlannerTask Task { get; set; } = null!;
    [JsonIgnore] public AppUser User { get; set; } = null!;
}
