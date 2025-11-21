// src/DistributedSolver.Domain/Services/IAuthService.cs

using DistributedSolver.Domain.Models;
using DistributedSolver.Domain.Dtos;

namespace DistributedSolver.Domain.Services
{
    public interface IAuthService
    {
        Task<UserModel> RegisterAsync(string email, string password);
        Task<string> LoginAsync(string email, string password);
        Task<UserModel?> GetUserByEmailAsync(string email);
        string GenerateToken(UserModel user);
        AuthResponse GenerateAuthResponse(UserModel user, string token);
    }
}