using DistributedSolver.Infrastructure.Persistence;
using DistributedSolver.Api.Workers;
using DistributedSolver.Infrastructure.Persistence.Repositories;
using DistributedSolver.Domain.Services;
using DistributedSolver.Domain.Models;
using DistributedSolver.Domain.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// -------------------------------------------------------------
// 1) Конфігурація БД та Сервісів
// -------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);
});

// Реєстрація Сервісів (DI)
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<DistributedSolver.Domain.Repositories.IUserRepository, DistributedSolver.Infrastructure.Persistence.Repositories.UserRepository>();
builder.Services.AddHostedService<TaskQueueWorker>();

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();

// -------------------------------------------------------------
// 2) КОНФІГУРАЦІЯ JWT 
// -------------------------------------------------------------
var authSettings = builder.Configuration.GetSection("AuthSettings");
var key = Encoding.ASCII.GetBytes(authSettings["Secret"]!);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = authSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = authSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// -------------------------------------------------------------
// 3) КОНФІГУРАЦІЯ SWAGGER для JWT
// -------------------------------------------------------------
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Distributed Solver API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// -------------------------------------------------------------
// 4) Автоматичне застосування міграцій при старті 
// -------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    const int maxRetries = 5;

    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            logger.LogInformation("Attempting database migration (Attempt {Attempt}/{MaxRetries})...", i + 1, maxRetries);

            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();

            try
            {
                var testUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
                var userExists = db.Users.Any(u => u.Id == testUserId);
                if (!userExists)
                {
                    db.Users.Add(new UserModel
                    {
                        Id = testUserId,
                        Email = "test@user.com",
                        PasswordHash = "password",
                        Role = "User"
                    });
                    db.SaveChanges();
                    logger.LogInformation("Seeded test user {Email} with id {Id}", "test@user.com", testUserId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed test user (non-fatal).");
            }

            try
            {
                var adminEmail = "sniz@gmail.com";
                var adminExists = db.Users.Any(u => u.Email == adminEmail);
                if (!adminExists)
                {
                    var passwordHash = BCrypt.Net.BCrypt.HashPassword("11111111");
                    db.Users.Add(new UserModel
                    {
                        Id = Guid.NewGuid(),
                        Email = adminEmail,
                        PasswordHash = passwordHash,
                        Role = "Admin",
                        CreatedAt = DateTime.UtcNow
                    });
                    db.SaveChanges();
                    logger.LogInformation("Seeded admin user {Email}", adminEmail);
                }
                else
                {
                    var admin = db.Users.FirstOrDefault(u => u.Email == adminEmail);
                    if (admin != null && admin.Role != "Admin")
                    {
                        admin.Role = "Admin";
                        db.SaveChanges();
                        logger.LogInformation("Updated user {Email} role to Admin", adminEmail);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed admin user (non-fatal).");
            }
            logger.LogInformation("Database migration completed successfully.");
            break;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database migration failed. Retrying in 5 seconds...");

            if (i < maxRetries - 1)
            {
                System.Threading.Thread.Sleep(5000);
            }
            else
            {
                logger.LogError("All database migration attempts failed after {MaxRetries} retries.", maxRetries);
                throw;
            }
        }
    }
}
// -------------------------------------------------------------
//Вмикає обслуговування статичних файлів (HTML, CSS, JS, зображення)
app.UseDefaultFiles();
app.UseStaticFiles();

// -------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages();

app.Run();