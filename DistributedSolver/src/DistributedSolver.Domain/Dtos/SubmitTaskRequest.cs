namespace DistributedSolver.Domain.Dtos;

// Запит для задачі N-Queens. `N` повинен бути в межах від 1 до 17 включно.
public class SubmitTaskRequest
{
    // Розмір дошки N (кількість ферзів)
    public int N { get; set; }
}