using DistributedSolver.Domain.Models;

namespace DistributedSolver.Domain.Repositories;

public interface IUserRepository
{
    Task<UserModel?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddAsync(UserModel user, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserModel user, CancellationToken cancellationToken = default);
}
