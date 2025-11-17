namespace DistributedSolver.Domain.Models;

public class TaskModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public UserModel? User { get; set; } // Навігаційна властивість

    // Вхідні дані для СЛАР
    public string InputMatrixA { get; set; } = string.Empty;
    public string InputVectorB { get; set; } = string.Empty;
    public int MatrixSize { get; set; }

    // Стан завдання
    public string Status { get; set; } = "PENDING"; // PENDING, RUNNING, DONE, CANCELLED, ERROR
    public int ProgressPercent { get; set; } = 0;

    // Результат
    public string? ResultVectorX { get; set; }

    // Метадані
    public DateTime TimeCreated { get; set; } = DateTime.Now;
    public DateTime? TimeFinished { get; set; }
}