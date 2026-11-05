using Microsoft.EntityFrameworkCore;
using TaskPlanner.Data.Entities;
using TaskPlanner.Helpers;

namespace TaskPlanner.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(PlannerDbContext db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var anna = CreateUser("anna@mail.ru", "Анна", "Anna1234");
        var boris = CreateUser("boris@mail.ru", "Борис", "Boris1234");
        db.Users.AddRange(anna, boris);
        await db.SaveChangesAsync();

        var now = DateTime.Now;
        var report = new PlannerTask
        {
            OwnerId = anna.Id,
            Title = "Сдать отчёт по JSON",
            Description = "Проверить сериализацию товара и пример Company.",
            DueAt = now.AddMinutes(40),
            CreatedAt = now.AddDays(-1),
            IsCompleted = false
        };
        var lecture = new PlannerTask
        {
            OwnerId = anna.Id,
            Title = "Подготовиться к занятию по OAuth",
            Description = "Повторить роли и authorization code flow.",
            DueAt = now.AddDays(1).Date.AddHours(10),
            CreatedAt = now.AddHours(-5),
            IsCompleted = false
        };
        var done = new PlannerTask
        {
            OwnerId = anna.Id,
            Title = "Установить PostgreSQL",
            DueAt = now.AddDays(-1),
            CreatedAt = now.AddDays(-3),
            IsCompleted = true,
            CompletedAt = now.AddDays(-1)
        };
        var shared = new PlannerTask
        {
            OwnerId = boris.Id,
            Title = "Собрать плейлист к аттестации",
            Description = "Пять демонстрационных треков и визуализация.",
            DueAt = now.AddMinutes(90),
            CreatedAt = now.AddHours(-2),
            IsCompleted = false
        };

        db.Tasks.AddRange(report, lecture, done, shared);
        await db.SaveChangesAsync();
        db.Shares.Add(new TaskShare { TaskId = shared.Id, UserId = anna.Id });
        await db.SaveChangesAsync();
    }

    private static AppUser CreateUser(string email, string name, string password)
    {
        var salt = PasswordHelper.GetSecureSalt();
        return new AppUser
        {
            Email = email,
            DisplayName = name,
            PasswordSalt = Convert.ToBase64String(salt),
            PasswordHash = PasswordHelper.Hash(password, salt),
            CreatedAt = DateTime.Now
        };
    }
}
