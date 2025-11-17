using System.Text.Json;
using DistributedSolver.Domain.Models;
using DistributedSolver.Infrastructure.Persistence.Repositories;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using static DistributedSolver.Domain.Enums.TaskStatus;
using Vector = MathNet.Numerics.LinearAlgebra.Vector<double>;

namespace DistributedSolver.Api.Workers;

// IHostedService / BackgroundService — це стандартний спосіб створення фонових потоків у ASP.NET Core
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
                // Для роботи з DbContext та репозиторіями нам потрібна нова область видимості (scope), 
                // оскільки BackgroundService є Singleton, а репозиторій — Scoped.
                using (var scope = _serviceProvider.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<ITaskRepository>();

                    // 1. Спроба захоплення завдання з черги (використовує SKIP LOCKED)
                    var task = await repository.TryGetNextPendingTaskAsync(stoppingToken);

                    if (task != null)
                    {
                        _logger.LogInformation("Processing Task {TaskId} (Size: {Size}).", task.Id, task.MatrixSize);

                        // 2. Виконання трудомісткої задачі (СЛАР)
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

    // TODO: Реалізувати метод обчислення СЛАР
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

            // 2. Десеріалізація вхідних даних
            var A_data = JsonSerializer.Deserialize<double[][]>(task.InputMatrixA);
            var B_data = JsonSerializer.Deserialize<double[]>(task.InputVectorB);

            if (A_data == null || B_data == null)
            {
                throw new InvalidOperationException("Invalid matrix or vector data found.");
            }

            // 3. Перетворення даних у формат MathNet.Numerics
            var matrixA = Matrix<double>.Build.DenseOfRows(A_data);
            var vectorB = Vector<double>.Build.Dense(B_data);

            // 4. Оновлення статусу виконання (прогрес 5%) - Початок обчислення
            task.ProgressPercent = 5;
            await repository.UpdateTaskAsync(task, cancellationToken);

            // 5. Обчислення розв'язку (високе CPU-навантаження)
            // Math.NET використовує LU-декомпозицію, яка є швидкою і ефективною.
            Vector<double> vectorX = matrixA.Solve(vectorB);

            // 6. Оновлення статусу виконання (прогрес 50%)
            task.ProgressPercent = 50;
            await repository.UpdateTaskAsync(task, cancellationToken);

            // 7. Перевірка скасування (після тривалого обчислення, якщо б воно було ітераційним)
            // Хоча для Math.NET це один крок, це залишається як шаблон для переривання.
            var finalCheckTask = await repository.GetTaskByIdAsync(task.Id, cancellationToken);
            if (finalCheckTask?.Status == CANCEL_REQUESTED)
            {
                task.Status = CANCELLED;
                task.TimeFinished = DateTime.UtcNow;
                await repository.UpdateTaskAsync(task, cancellationToken);
                _logger.LogWarning("Task {TaskId} cancelled right after computation.", task.Id);
                return;
            }

            // 8. Фіналізація: Серіалізація результату
            var resultJson = JsonSerializer.Serialize(vectorX.ToArray());

            task.Status = DONE;
            task.ProgressPercent = 100;
            task.ResultVectorX = resultJson;
            task.TimeFinished = DateTime.UtcNow;

            await repository.UpdateTaskAsync(task, cancellationToken);
            _logger.LogInformation("Task {TaskId} completed successfully.", task.Id);
        }
        catch (Exception ex) when (ex.Message.Contains("singular"))
        {
            // 9. Обробка помилок (Наприклад, матриця вироджена, і розв'язку немає)
            _logger.LogError(ex, "Task {TaskId} failed due to singular matrix.", task.Id);

            task.Status = ERROR;
            task.TimeFinished = DateTime.UtcNow;
            task.ResultVectorX = "ERROR: Matrix is singular (determinant is zero) or computationally near-singular.";

            await repository.UpdateTaskAsync(task, cancellationToken);
        }
        catch (Exception ex)
        {
            // 10. Загальна обробка помилок
            _logger.LogError(ex, "Task {TaskId} failed due to an unexpected error.", task.Id);

            task.Status = ERROR;
            task.TimeFinished = DateTime.UtcNow;
            task.ResultVectorX = $"ERROR: {ex.Message}";

            await repository.UpdateTaskAsync(task, cancellationToken);
        }
    }
}