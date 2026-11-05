using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskPlanner.Services;

namespace TaskPlanner.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthServer auth;

    public AuthController(AuthServer auth)
    {
        this.auth = auth;
    }

    public record LoginBody(string Email, string Password);
    public record RegisterBody(string Email, string DisplayName, string Password);
    public record RefreshBody(int UserId, string RefreshToken);

    [HttpPost("makeAuthRequest")]
    public async Task<IActionResult> MakeAuthRequest(LoginBody body)
    {
        var tokens = await auth.MakeAuthRequest(body.Email, body.Password);
        if (tokens == null)
        {
            return Unauthorized(new { error = "Неверная почта или пароль." });
        }

        return Ok(tokens);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterBody body)
    {
        var result = await auth.RegisterAsync(body.Email, body.DisplayName, body.Password);
        if (!result.Ok)
        {
            return UnprocessableEntity(new { error = result.Error });
        }

        return Ok(result.Tokens);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshBody body)
    {
        var tokens = await auth.RefreshAsync(body.UserId, body.RefreshToken);
        if (tokens == null)
        {
            return Unauthorized(new { error = "Сессия истекла. Войдите снова." });
        }

        return Ok(tokens);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await auth.LogoutAsync(UserId());
        return Ok();
    }

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("nameid"));
}
