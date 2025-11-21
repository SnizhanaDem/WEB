// src/DistributedSolver.Infrastructure/Persistence/Repositories/ITaskRepository.cs

using DistributedSolver.Domain.Models;

namespace DistributedSolver.Infrastructure.Persistence.Repositories
{
    public interface ITaskRepository
    {
        /// <summary>
        /// Спроба отримати наступне завдання зі статусом PENDING для обробки.
        /// </summary>
        /// <returns>Завдання, яке потрібно обробити, або null, якщо завдання відсутні.</returns>
        Task<TaskModel?> TryGetNextPendingTaskAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Зберігає зміни у завданні (оновлення статусу, прогресу або результату).
        /// </summary>
        Task UpdateTaskAsync(TaskModel task, CancellationToken cancellationToken = default);
        /// <summary>
        /// Отримати деталі завдання за ідентифікатором.
        /// </summary>
        Task<TaskModel?> GetTaskByIdAsync(Guid taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отримати всі завдання, створені користувачем.
        /// </summary>
        Task<IEnumerable<TaskModel>> GetTasksByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        /// <summary>
        /// Встановлює статус завдання на CANCEL_REQUESTED.
        /// </summary>
        Task<bool> TryRequestCancelAsync(Guid taskId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Отримати всі задачі в системі (для адміністратора).
        /// </summary>
        Task<IEnumerable<TaskModel>> GetAllTasksAsync(CancellationToken cancellationToken = default);
    }
}