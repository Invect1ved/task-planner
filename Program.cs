using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskPlanner.Data;
using TaskPlanner.Helpers;
using TaskPlanner.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<PlannerDbContext>(options =>
    options.UseNpgsql(ResolveConnectionString(builder.Configuration)));
builder.Services.AddScoped<AuthServer>();
builder.Services.AddScoped<TaskBoardService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = TokenHelper.Issuer,
            ValidAudience = TokenHelper.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(TokenHelper.Key),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
    try
    {
        await DbInitializer.InitializeAsync(db);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Не удалось открыть базу planner_db в PostgreSQL. " +
            "Впишите пароль пользователя postgres в ConnectionStrings:PlannerDb или задайте переменную PGPASSWORD.",
            ex);
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

static string ResolveConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("PlannerDb")
        ?? throw new InvalidOperationException("Не задана строка PlannerDb.");
    var password = Environment.GetEnvironmentVariable("PGPASSWORD");
    if (!string.IsNullOrEmpty(password) && connectionString.EndsWith("Password=", StringComparison.Ordinal))
    {
        connectionString += password;
    }

    return connectionString;
}
