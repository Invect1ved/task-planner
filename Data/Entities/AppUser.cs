using System.Text.Json.Serialization;

namespace TaskPlanner.Data.Entities;

public class AppUser
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    [JsonIgnore] public string PasswordHash { get; set; } = "";
    [JsonIgnore] public string PasswordSalt { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    [JsonIgnore] public ICollection<PlannerTask> OwnedTasks { get; set; } = new List<PlannerTask>();
    [JsonIgnore] public ICollection<TaskShare> Shares { get; set; } = new List<TaskShare>();
}
