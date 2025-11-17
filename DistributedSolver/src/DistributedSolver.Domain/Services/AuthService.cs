// src/DistributedSolver.Domain/Services/AuthService.cs

using DistributedSolver.Domain.Models;
using DistributedSolver.Domain.Dtos;
using System.Security.Claims; // <--- Потрібно для ClaimTypes та Claim
using System.Text;
using Microsoft.IdentityModel.Tokens; // <--- Потрібно для SymmetricSecurityKey, SigningCredentials
using System.IdentityModel.Tokens.Jwt; // <--- Потрібно для JwtSecurityToken, JwtRegisteredClaimNames, JwtSecurityTokenHandler
using Microsoft.Extensions.Configuration;

namespace DistributedSolver.Domain.Services
{
    public class AuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly DistributedSolver.Domain.Repositories.IUserRepository _userRepository;

        public AuthService(IConfiguration configuration, DistributedSolver.Domain.Repositories.IUserRepository userRepository)
        {
            _configuration = configuration;
            _userRepository = userRepository;
        }

        public async Task<UserModel> RegisterAsync(string email, string password)
        {
            var existing = await _userRepository.GetByEmailAsync(email);
            if (existing != null)
            {
                throw new InvalidOperationException("User with this email already exists.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var user = new UserModel
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = passwordHash,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(user);
            return user;
        }

        public async Task<string> LoginAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid credentials.");
            }

            return GenerateJwtToken(user);
        }

        private string GenerateJwtToken(UserModel user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["AuthSettings:Secret"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.Id.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: _configuration["AuthSettings:Issuer"],
                audience: _configuration["AuthSettings:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateToken(UserModel user)
        {
            return GenerateJwtToken(user);
        }

        public AuthResponse GenerateAuthResponse(UserModel user, string token)
        {
            return new AuthResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Token = token,
                Role = user.Role
            };
        }
    }
}