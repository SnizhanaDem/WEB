using DistributedSolver.Domain.Dtos;
using DistributedSolver.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using DistributedSolver.Domain.Enums;
using System.Text.Json;
using static DistributedSolver.Domain.Enums.TaskStatus;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Principal;

namespace DistributedSolver.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TaskController : ControllerBase
{
    private readonly ITaskRepository _repository;
    private const int MaxMatrixSize = 17;

    public TaskController(ITaskRepository repository)
    {
        _repository = repository;
    }

    [HttpGet("queueinfo")]
    public async Task<IActionResult> GetQueueInfo()
    {
        var allTasks = await _repository.GetAllTasksAsync();
        int pendingCount = allTasks.Count(t => t.Status == PENDING);
        int processingCount = allTasks.Count(t => t.Status == PROCESSING);

        // Розраховуємо середній час виконання на основі завершених завдань
        var completedTasks = allTasks
            .Where(t => t.Status == DONE && t.TimeFinished.HasValue)
            .Select(t => new { ExecutionTime = (t.TimeFinished!.Value - t.TimeCreated).TotalSeconds })
            .ToList();

        double averageExecutionTime = 5.0;
        if (completedTasks.Any())
        {
            var sorted = completedTasks.Select(t => t.ExecutionTime).OrderBy(x => x).ToList();
            if (sorted.Count > 0)
                averageExecutionTime = sorted[sorted.Count / 2];
        }

        int activeServers = 2;
        int tasksPerServer = (pendingCount + processingCount) / activeServers;
        int estimatedSeconds = (int)(tasksPerServer * averageExecutionTime);
        string estimatedWait = estimatedSeconds > 60
            ? $"~ {estimatedSeconds / 60} хв {estimatedSeconds % 60} сек"
            : $"~ {estimatedSeconds} сек";

        return Ok(new
        {
            pendingTasks = pendingCount,
            processingTasks = processingCount,
            estimatedWait = estimatedWait,
            averageExecutionTime = Math.Round(averageExecutionTime, 2)
        });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("UserId");

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("User ID claim is missing or invalid.");
    }

    [HttpPost("submit")]
    public async Task<IActionResult> SubmitTask([FromBody] SubmitTaskRequest request)
    {

        var userId = GetUserId();

        if (request.N < 1 || request.N > MaxMatrixSize)
        {
            return BadRequest(new { Error = $"N must be between 1 and {MaxMatrixSize}." });
        }

        //Перевірка максимального числа активних задач
        var history = await _repository.GetTasksByUserIdAsync(userId);
        int activeTasks = history.Count(t => t.Status == PENDING || t.Status == PROCESSING);
        int maxActiveTasks = 5;
        if (activeTasks >= maxActiveTasks)
        {
            return BadRequest(new { Error = $"You have reached the maximum number of active tasks ({maxActiveTasks}). Please wait for existing tasks to complete." });
        }

        //Створення нового завдання для N-Queens
        var task = new DistributedSolver.Domain.Models.TaskModel
        {
            UserId = userId,
            MatrixSize = request.N,
            InputMatrixA = string.Empty,
            InputVectorB = string.Empty,
            Status = PENDING
        };

        await _repository.UpdateTaskAsync(task);

        return Accepted(new { TaskId = task.Id, Status = task.Status });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTaskStatus(Guid id)
    {
        var userId = GetUserId();
        var task = await _repository.GetTaskByIdAsync(id);

        if (task == null) return NotFound();

        if (task.UserId != userId)
        {
            return Forbid();
        }

        //Повернення статусу, прогресу та результату 
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
        var userId = GetUserId();

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

        if (task.UserId != userId)
        {
            return Forbid();
        }

        //Надсилання запиту на скасування до БД
        var success = await _repository.TryRequestCancelAsync(id);

        if (success)
        {
            return Accepted(new { Message = $"Cancellation request for Task {id} accepted." });
        }

        //Повернення помилки, якщо завдання не було в PROCESSING
        return BadRequest(new { Message = "Task cannot be cancelled because it is not currently processing or status is unknown.", Status = task.Status });
    }
}