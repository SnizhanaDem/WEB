using DistributedSolver.Domain.Models;
using Microsoft.EntityFrameworkCore;
using DistributedSolver.Infrastructure.Persistence;
// using DistributedSolver.Infrastructure.Persistence.Repositories; // Можна прибрати, оскільки ми вже в цьому namespace

// Додаємо повний шлях до констант статусу
using static DistributedSolver.Domain.Enums.TaskStatus;

namespace DistributedSolver.Infrastructure.Persistence.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly ApplicationDbContext _context;

        public TaskRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TaskModel?> TryGetNextPendingTaskAsync(CancellationToken cancellationToken = default)
        {
            // ... (Всі інші помилки виправлені, якщо ви використовуєте TaskModel)
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // Використовуємо константи:
                var sql = @$"
                    SELECT * FROM ""Tasks"" 
                    WHERE ""Status"" = '{PENDING}' 
                    ORDER BY ""TimeCreated"" 
                    FOR UPDATE SKIP LOCKED 
                    LIMIT 1";

                var task = await _context.Tasks
                    .FromSqlRaw(sql)
                    .FirstOrDefaultAsync(cancellationToken);

                if (task != null)
                {
                    // Використовуємо константи:
                    task.Status = PROCESSING;

                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return task;
                }

                await transaction.CommitAsync(cancellationToken);
                return null;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task UpdateTaskAsync(TaskModel task, CancellationToken cancellationToken = default)
        {
            // If the task already exists in the database, perform an update.
            // If it does not exist, add it as a new entity (used for SubmitTask).
            var exists = await _context.Tasks.AnyAsync(t => t.Id == task.Id, cancellationToken);
            if (exists)
            {
                _context.Tasks.Update(task);
            }
            else
            {
                await _context.Tasks.AddAsync(task, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        public async Task<TaskModel?> GetTaskByIdAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            // Отримання завдання з бази даних за його Id
            return await _context.Tasks
                .AsNoTracking() // Оскільки ми лише читаємо дані
                .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        }

        public async Task<IEnumerable<TaskModel>> GetTasksByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // Отримання історії завдань для конкретного користувача
            return await _context.Tasks
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.TimeCreated)
                .ToListAsync(cancellationToken);
        }
        public async Task<bool> TryRequestCancelAsync(Guid taskId, CancellationToken cancellationToken = default)
        {
            // Оскільки ми не можемо використовувати SKIP LOCKED тут, ми оновлюємо безпосередньо.
            // Завдання вже має бути у статусі PROCESSING, але ми оновлюємо його на CANCEL_REQUESTED.

            // 1. Знайти завдання
            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.Status == PROCESSING, cancellationToken);

            if (task == null)
            {
                // Не знайдено або вже не в обробці
                return false;
            }

            // 2. Оновити статус
            task.Status = CANCEL_REQUESTED;

            // 3. Зберегти зміни
            await _context.SaveChangesAsync(cancellationToken);
            return true;

        }
    }
}