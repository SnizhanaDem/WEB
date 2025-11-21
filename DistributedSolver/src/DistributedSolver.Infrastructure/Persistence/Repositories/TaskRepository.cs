using DistributedSolver.Domain.Models;
using Microsoft.EntityFrameworkCore;
using DistributedSolver.Infrastructure.Persistence;
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
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
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
                .AsNoTracking()
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
            try
            {
                var task = await _context.Tasks
                    .FirstOrDefaultAsync(t => t.Id == taskId && t.Status == PROCESSING, cancellationToken);

                if (task == null)
                {
                    return false;
                }

                task.Status = CANCEL_REQUESTED;

                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<TaskModel>> GetAllTasksAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Tasks
                .AsNoTracking()
                .OrderByDescending(t => t.TimeCreated)
                .ToListAsync(cancellationToken);
        }
    }
}