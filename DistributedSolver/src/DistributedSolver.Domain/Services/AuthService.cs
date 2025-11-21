// src/DistributedSolver.Domain/Services/AuthService.cs

using DistributedSolver.Domain.Models;
using DistributedSolver.Domain.Dtos;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
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
            // Валідація email та пароля
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email cannot be empty.", nameof(email));
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            email = email.Trim();

            // Валідація формату email
            if (!IsValidEmail(email))
            {
                throw new ArgumentException("Invalid email format. Expected: user@example.com", nameof(email));
            }

            // Валідація довжини email
            if (email.Length > 254)
            {
                throw new ArgumentException("Email is too long (max 254 characters).", nameof(email));
            }

            // Валідація довжини пароля
            if (password.Length < 6)
            {
                throw new ArgumentException("Password must be at least 6 characters.", nameof(password));
            }

            if (password.Length > 128)
            {
                throw new ArgumentException("Password is too long (max 128 characters).", nameof(password));
            }

            // Перевірка чи користувач вже існує
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

        // Допоміжний метод для валідації email
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
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

        public async Task<UserModel?> GetUserByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
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