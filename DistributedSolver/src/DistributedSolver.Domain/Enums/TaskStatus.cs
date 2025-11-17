// src/DistributedSolver.Domain/Enums/TaskStatus.cs

namespace DistributedSolver.Domain.Enums;

public static class TaskStatus
{
    public const string PENDING = "PENDING";
    public const string PROCESSING = "PROCESSING"; // <--- ПОТРІБНА ВАМ КОНСТАНТА
    public const string DONE = "DONE";
    public const string CANCELLED = "CANCELLED";
    public const string CANCEL_REQUESTED = "CANCEL_REQUESTED"; // <--- ПОТРІБНА ВАМ КОНСТАНТА
    public const string ERROR = "ERROR";
}