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
        try
        {
            var user = await _authService.RegisterAsync(request.Email, request.Password);
            // Після реєстрації одразу генеруємо JWT для новоствореного користувача
            var token = _authService.GenerateToken(user);

            // Повертаємо токен та дані користувача
            return Ok(_authService.GenerateAuthResponse(user, token));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Користувач вже існує
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Error = "An error occurred during registration: " + ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var token = await _authService.LoginAsync(request.Email, request.Password);

            // Отримуємо користувача для того, щоб повернути його роль
            var user = await _authService.GetUserByEmailAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new { Error = "User not found after login" });
            }

            return Ok(_authService.GenerateAuthResponse(user, token));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { Error = ex.Message });
        }
    }
}