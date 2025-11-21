using DistributedSolver.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using static DistributedSolver.Domain.Enums.TaskStatus;
using DistributedSolver.Domain.Repositories;

namespace DistributedSolver.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly ITaskRepository _repository;
    private readonly IUserRepository _userRepository;

    public AdminController(ITaskRepository repository, IUserRepository userRepository)
    {
        _repository = repository;
        _userRepository = userRepository;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var allTasks = await _repository.GetAllTasksAsync();
        int pending = allTasks.Count(t => t.Status == PENDING);
        int processing = allTasks.Count(t => t.Status == PROCESSING);
        int done = allTasks.Count(t => t.Status == DONE);
        int cancelled = allTasks.Count(t => t.Status == CANCELLED);
        int error = allTasks.Count(t => t.Status == ERROR);
        return Ok(new
        {
            pending = pending,
            processing = processing,
            done = done,
            cancelled = cancelled,
            error = error,
            total = allTasks.Count()
        });
    }

    [HttpGet("serverload")]
    public async Task<IActionResult> GetServerLoad()
    {
        var allTasks = await _repository.GetAllTasksAsync();
        int pending = allTasks.Count(t => t.Status == PENDING);
        int processing = allTasks.Count(t => t.Status == PROCESSING);

        // Розраховуємо середній час виконання на основі завершених завдань
        var completedTasks = allTasks
            .Where(t => t.Status == DONE && t.TimeFinished.HasValue)
            .Select(t => new { ExecutionTime = (t.TimeFinished!.Value - t.TimeCreated).TotalSeconds })
            .ToList();


        double averageExecutionTime = 5.0;
        if (completedTasks.Any())
        {
            averageExecutionTime = completedTasks.Average(t => t.ExecutionTime);
            var sorted = completedTasks.Select(t => t.ExecutionTime).OrderBy(x => x).ToList();
            if (sorted.Count > 0)
                averageExecutionTime = sorted[sorted.Count / 2];
        }

        int activeServers = 2;
        int tasksPerServer = (pending + processing) / activeServers;
        int estimatedWait = (int)(tasksPerServer * averageExecutionTime);

        return Ok(new
        {
            activeServers = activeServers,
            processingTasks = processing,
            pendingTasks = pending,
            averageExecutionTime = Math.Round(averageExecutionTime, 2),
            estimatedQueueWait = estimatedWait
        });
    }

    [HttpPost("user/{email}/role/{role}")]
    public async Task<IActionResult> SetUserRole(string email, string role)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return NotFound(new { Error = $"User with email '{email}' not found" });
        }

        if (role != "Admin" && role != "User")
        {
            return BadRequest(new { Error = "Role must be 'Admin' or 'User'" });
        }

        user.Role = role;
        await _userRepository.UpdateAsync(user);

        return Ok(new { Message = $"User '{email}' role changed to '{role}'", User = new { user.Id, user.Email, user.Role } });
    }
}

