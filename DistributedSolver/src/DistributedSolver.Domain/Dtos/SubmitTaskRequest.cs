namespace DistributedSolver.Domain.Dtos;

public class SubmitTaskRequest
{
    // Розмірність матриці, використовується для валідації N <= N_max
    public int MatrixSize { get; set; }

    // Матриця коефіцієнтів A (наприклад, [[1.0, 2.0], [3.0, 4.0]])
    public double[][]? MatrixA { get; set; }

    // Вектор вільних членів B
    public double[]? VectorB { get; set; }
}