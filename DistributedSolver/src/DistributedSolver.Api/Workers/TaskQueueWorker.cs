using System.Text.Json;
using DistributedSolver.Domain.Models;
using DistributedSolver.Infrastructure.Persistence.Repositories;
using static DistributedSolver.Domain.Enums.TaskStatus;

namespace DistributedSolver.Api.Workers;

public class TaskQueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TaskQueueWorker> _logger;

    public TaskQueueWorker(IServiceProvider serviceProvider, ILogger<TaskQueueWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task Queue Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {

                using (var scope = _serviceProvider.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

                    // 1. Спроба захоплення завдання з черги 
                    var task = await repository.TryGetNextPendingTaskAsync(stoppingToken);

                    if (task != null)
                    {
                        _logger.LogInformation("Processing Task {TaskId} (Size: {Size}).", task.Id, task.MatrixSize);

                        // 2. Виконання трудомісткої задачі 
                        await ProcessTaskAsync(task, repository, stoppingToken);

                        _logger.LogInformation("Task {TaskId} completed.", task.Id);
                    }
                    else
                    {
                        // 3. Якщо черга порожня, чекаємо 5 секунд
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled error occurred while processing the task queue.");
                // У разі критичної помилки, чекаємо довше перед повторною спробою
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
        _logger.LogInformation("Task Queue Worker stopped.");
    }

    private async Task ProcessTaskAsync(TaskModel task, ITaskRepository repository, CancellationToken cancellationToken)
    {
        // Оскільки task.Status вже встановлено на PROCESSING у репозиторії, ми одразу починаємо обчислення.
        try
        {
            _logger.LogInformation("Processing Task {TaskId} (Size: {Size}).", task.Id, task.MatrixSize);

            // 1. ПЕРЕВІРКА СКАСУВАННЯ (На початку)
            // Якщо користувач натиснув "Скасувати" до початку обчислення.
            var initialCheckTask = await repository.GetTaskByIdAsync(task.Id, cancellationToken);
            if (initialCheckTask?.Status == CANCEL_REQUESTED)
            {
                task.Status = CANCELLED;
                task.TimeFinished = DateTime.UtcNow;
                task.ProgressPercent = 0;
                await repository.UpdateTaskAsync(task, cancellationToken);
                _logger.LogWarning("Task {TaskId} cancelled by user before computation start.", task.Id);
                return;
            }

            // 2. Обчислення N-Queens (кількість можливих розв'язків)
            int n = task.MatrixSize;

            // Оновлення прогресу на початку
            task.ProgressPercent = 5;
            await repository.UpdateTaskAsync(task, cancellationToken);

            // Плавний прогрес
            long solutions = 0;
            solutions = await CountNQueensWithProgress(n, cancellationToken, async percent =>
            {
                if (percent < 100)
                {
                    task.ProgressPercent = percent;
                    await repository.UpdateTaskAsync(task, cancellationToken);
                }
            });

            // Перевірка скасування після обчислення
            var finalCheckTask = await repository.GetTaskByIdAsync(task.Id, cancellationToken);
            if (finalCheckTask?.Status == CANCEL_REQUESTED)
            {
                task.Status = CANCELLED;
                task.TimeFinished = DateTime.UtcNow;
                await repository.UpdateTaskAsync(task, cancellationToken);
                _logger.LogWarning("Task {TaskId} cancelled right after computation.", task.Id);
                return;
            }

            // Фіналізація
            task.Status = DONE;
            task.ProgressPercent = 100;
            task.ResultVectorX = JsonSerializer.Serialize(solutions);
            task.TimeFinished = DateTime.UtcNow;

            await repository.UpdateTaskAsync(task, cancellationToken);
            _logger.LogInformation("Task {TaskId} completed successfully. Solutions: {Count}", task.Id, solutions);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Task {TaskId} processing was cancelled by token.", task.Id);
            task.Status = CANCELLED;
            task.TimeFinished = DateTime.UtcNow;
            await repository.UpdateTaskAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Task {TaskId} failed due to an unexpected error.", task.Id);

            task.Status = ERROR;
            task.TimeFinished = DateTime.UtcNow;
            task.ResultVectorX = $"ERROR: {ex.Message}";

            await repository.UpdateTaskAsync(task, cancellationToken);
        }
    }

    // Плавний прогрес для N-Queens
    private async Task<long> CountNQueensWithProgress(int n, CancellationToken cancellationToken, Func<int, Task> progressCallback)
    {
        if (n <= 0) { await progressCallback(100); return 0; }
        if (n == 1) { await progressCallback(100); return 1; }

        long count = 0;
        ulong all = (1UL << n) - 1UL;
        long totalIterations = 0;
        long solutionsFound = 0;
        int lastPercent = 0;

        async Task Solve(ulong cols, ulong d1, ulong d2, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (cols == all)
            {
                count++;
                solutionsFound++;
                int percent = Math.Min(95, 10 + (int)(solutionsFound * 85 / Math.Max(1, GetEstimatedSolutions(n))));
                if (percent > lastPercent && progressCallback != null)
                {
                    lastPercent = percent;
                    await progressCallback(percent);
                }
                return;
            }

            ulong avail = ~(cols | d1 | d2) & all;
            while (avail != 0)
            {
                ulong bit = avail & (ulong)(-(long)avail);
                avail -= bit;
                totalIterations++;

                await Solve(cols | bit, (d1 | bit) << 1, (d2 | bit) >> 1, depth + 1);
            }
        }

        await Solve(0, 0, 0, 0);
        await progressCallback(100);
        return count;
    }

    // Відома кількість рішень для N-Queens
    private long GetEstimatedSolutions(int n) => n switch
    {
        1 => 1,
        2 => 0,
        3 => 0,
        4 => 2,
        5 => 10,
        6 => 4,
        7 => 40,
        8 => 92,
        9 => 352,
        10 => 724,
        11 => 2680,
        12 => 14200,
        13 => 73712,
        14 => 365596,
        15 => 2279184,
        16 => 14772512,
        17 => 95815104,
        _ => (long)Math.Pow(n, n) // приблизна оцінка для великих N
    };
}