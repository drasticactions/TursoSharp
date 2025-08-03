using TursoSharpExample.Models;

namespace TursoSharpExample.Services;

public interface IDatabaseService : IDisposable
{
    bool IsConnected { get; }
    string ConnectionType { get; }
    
    event Action<string>? StatusChanged;
    event Action<string, TimeSpan>? OperationCompleted;
    
    Task InitializeMemoryDatabaseAsync();
    Task InitializeFileDatabaseAsync(string path);
    
    Task<List<TaskItem>> GetAllTasksAsync();
    Task<int> AddTaskAsync(TaskItem task);
    Task<int> GetTaskCountAsync();
    Task<int> GetCompletedTaskCountAsync();
    Task DeleteTaskAsync(int taskId);
    Task UpdateTaskAsync(TaskItem task);
    
    Task<TimeSpan> RunPerformanceTestAsync(int operationCount = 1000);
    Task TestErrorHandlingAsync();
}