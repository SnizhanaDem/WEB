using DistributedSolver.Domain.Dtos;
using DistributedSolver.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using DistributedSolver.Domain.Enums;
using System.Text.Json;
using static DistributedSolver.Domain.Enums.TaskStatus;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims; // Для роботи з claims
using System.Security.Principal; // Для IIdentity

namespace DistributedSolver.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // КРИТИЧНО! Захищає весь контролер (Вимога 4)
public class TaskController : ControllerBase
{
    private readonly ITaskRepository _repository;
    private const int MaxMatrixSize = 5000; // Наш ліміт N_max (Вимога 1)

    public TaskController(ITaskRepository repository)
    {
        _repository = repository;
    }

    // Допоміжний метод для отримання UserId з JWT-токена
    private Guid GetUserId()
    {
        // Отримуємо claim, який ми додали під час генерації токена в AuthService
        var userIdClaim = User.FindFirst("UserId");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        // У разі помилки (що не повинно статися, якщо токен валідний)
        throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {
        var userId = GetUserId(); // ОТРИМУЄМО РЕАЛЬНИЙ USER ID

        // 1. ПЕРЕВІРКА ЛІМІТУ (Вимога 1)
        if (request.MatrixSize > MaxMatrixSize)
        {
            return BadRequest(new { Error = $"Matrix size ({request.MatrixSize}) exceeds the maximum allowed limit of {MaxMatrixSize}." });
        }

        // 2. Валідація розмірності (повна валідація, яку ми outline'или)
        if (request.MatrixA == null || request.VectorB == null ||
            request.MatrixA.Length != request.MatrixSize || request.VectorB.Length != request.MatrixSize)
        {
            return BadRequest(new { Error = "Matrix A and Vector B must match the specified MatrixSize." });
        }

        // 3. Створення нового завдання
        var task = new DistributedSolver.Domain.Models.TaskModel
        {
            UserId = userId, // ВИКОРИСТОВУЄМО РЕАЛЬНИЙ USER ID (Вимога 3)
            MatrixSize = request.MatrixSize,
            InputMatrixA = System.Text.Json.JsonSerializer.Serialize(request.MatrixA),
            InputVectorB = System.Text.Json.JsonSerializer.Serialize(request.VectorB),
            Status = PENDING
        };

        await _repository.UpdateTaskAsync(task);

        // 4. Повернення 202 Accepted (Вимога асинхронності)
        return Accepted(new { TaskId = task.Id, Status = task.Status });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTaskStatus(Guid id)
    {
        var userId = GetUserId();
        var task = await _repository.GetTaskByIdAsync(id);

        if (task == null) return NotFound();

        // 1. ПЕРЕВІРКА ВЛАСНОСТІ: Користувач може бачити лише свої завдання!
        if (task.UserId != userId)
        {
            return Forbid(); // HTTP 403
        }

        // 2. Повернення статусу, прогресу та результату (Вимога 2, 3)
        return Ok(new
        {
            TaskId = task.Id,
            Status = task.Status,
            Progress = task.ProgressPercent,
            TimeCreated = task.TimeCreated,
            Result = task.Status == DONE ? task.ResultVectorX : null,
            Error = task.Status == ERROR ? task.ResultVectorX : null
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetTaskHistory()
    {
        var userId = GetUserId(); // Отримання ID

        // Отримати всі завдання для цього користувача (Вимога 3)
        var history = await _repository.GetTasksByUserIdAsync(userId);

        // Форматування історії для клієнта
        var summary = history.Select(t => new
        {
            t.Id,
            t.Status,
            t.ProgressPercent,
            t.TimeCreated,
            t.MatrixSize
        }).OrderByDescending(t => t.TimeCreated);

        return Ok(summary);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelTask(Guid id)
    {
        var userId = GetUserId();
        var task = await _repository.GetTaskByIdAsync(id);

        if (task == null) return NotFound();

        // 1. ПЕРЕВІРКА ВЛАСНОСТІ перед скасуванням
        if (task.UserId != userId)
        {
            return Forbid();
        }

        // 2. Надсилання запиту на скасування до БД
        var success = await _repository.TryRequestCancelAsync(id);

        if (success)
        {
            return Accepted(new { Message = $"Cancellation request for Task {id} accepted." });
        }

        // 3. Повернення помилки, якщо завдання не було в PROCESSING
        return BadRequest(new { Message = "Task cannot be cancelled because it is not currently processing or status is unknown.", Status = task.Status });
    }
}