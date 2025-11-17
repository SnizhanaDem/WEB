namespace DistributedSolver.Domain.Models;

public class UserModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // User / Admin
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskModel> Tasks { get; set; } = new List<TaskModel>();
}