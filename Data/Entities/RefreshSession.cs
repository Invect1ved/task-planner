namespace TaskPlanner.Data.Entities;

public class RefreshSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public string TokenSalt { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public AppUser User { get; set; } = null!;
}
