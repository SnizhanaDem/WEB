// src/DistributedSolver.Api/Controllers/AuthController.cs

using DistributedSolver.Domain.Dtos;
using DistributedSolver.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace DistributedSolver.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var user = await _authService.RegisterAsync(request.Email, request.Password);
        // Після реєстрації одразу генеруємо JWT для новоствореного користувача
        var token = _authService.GenerateToken(user);

        // Повертаємо токен та дані користувача
        return Ok(_authService.GenerateAuthResponse(user, token));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var token = await _authService.LoginAsync(request.Email, request.Password);

            // Для повного AuthResponse потрібно отримати UserModel з БД, тут спрощено
            var user = new DistributedSolver.Domain.Models.UserModel { Id = Guid.NewGuid(), Email = request.Email, Role = "User" };

            return Ok(_authService.GenerateAuthResponse(user, token));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Error = ex.Message });
        }
    }
}