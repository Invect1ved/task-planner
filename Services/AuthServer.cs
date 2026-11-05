using Microsoft.EntityFrameworkCore;
using TaskPlanner.Data;
using TaskPlanner.Data.Entities;
using TaskPlanner.Helpers;

namespace TaskPlanner.Services;

public record TokenPair(string AccessToken, string RefreshToken, int UserId, string Email, string Name);

public class AuthServer
{
    private readonly PlannerDbContext db;

    public AuthServer(PlannerDbContext db)
    {
        this.db = db;
    }

    public async Task<TokenPair?> MakeAuthRequest(string email, string password)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email.Trim().ToLowerInvariant());
        if (user == null)
        {
            return null;
        }

        var hash = PasswordHelper.Hash(password, Convert.FromBase64String(user.PasswordSalt));
        if (user.PasswordHash != hash)
        {
            return null;
        }

        return await IssueAsync(user);
    }

    public async Task<(bool Ok, string? Error, TokenPair? Tokens)> RegisterAsync(string email, string name, string password)
    {
        email = email.Trim().ToLowerInvariant();
        if (password.Length < 8)
        {
            return (false, "Пароль должен быть не короче 8 символов.", null);
        }

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return (false, "Пользователь с такой почтой уже есть.", null);
        }

        var salt = PasswordHelper.GetSecureSalt();
        var user = new AppUser
        {
            Email = email,
            DisplayName = name.Trim(),
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = PasswordHelper.Hash(password, salt),
            CreatedAt = DateTime.Now
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (true, null, await IssueAsync(user));
    }

    public async Task<TokenPair?> RefreshAsync(int userId, string refreshToken)
    {
        var sessions = await db.RefreshSessions.Where(s => s.UserId == userId).ToListAsync();
        RefreshSession? match = null;
        foreach (var session in sessions)
        {
            var hash = PasswordHelper.Hash(refreshToken, Convert.FromBase64String(session.TokenSalt));
            if (hash == session.TokenHash)
            {
                match = session;
                break;
            }
        }

        if (match == null || match.ExpiresAt < DateTime.Now)
        {
            return null;
        }

        var user = await db.Users.FindAsync(userId);
        if (user == null)
        {
            return null;
        }

        db.RefreshSessions.Remove(match);
        await db.SaveChangesAsync();
        return await IssueAsync(user);
    }

    public async Task LogoutAsync(int userId)
    {
        var sessions = await db.RefreshSessions.Where(s => s.UserId == userId).ToListAsync();
        db.RefreshSessions.RemoveRange(sessions);
        await db.SaveChangesAsync();
    }

    private async Task<TokenPair> IssueAsync(AppUser user)
    {
        var refresh = TokenHelper.CreateRefreshToken();
        var salt = PasswordHelper.GetSecureSalt();
        db.RefreshSessions.Add(new RefreshSession
        {
            UserId = user.Id,
            TokenHash = PasswordHelper.Hash(refresh, salt),
            TokenSalt = Convert.ToBase64String(salt),
            ExpiresAt = DateTime.Now.AddDays(7)
        });
        await db.SaveChangesAsync();
        return new TokenPair(
            TokenHelper.CreateAccessToken(user.Id, user.Email, user.DisplayName),
            refresh,
            user.Id,
            user.Email,
            user.DisplayName);
    }
}
