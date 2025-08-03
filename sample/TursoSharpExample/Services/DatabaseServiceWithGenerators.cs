using System.Collections.ObjectModel;
using System.Diagnostics;
using TursoSharp;
using TursoSharpExample.Models;
using TursoSharpExample.Entities;

namespace TursoSharpExample.Services;

public class DatabaseServiceWithGenerators : IDatabaseService
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;
    private TaskEntityRepository? _taskRepository;
    private bool _disposed;

    public bool IsConnected => _connection != null;
    public string ConnectionType { get; private set; } = "None";

    public event Action<string>? StatusChanged;
    public event Action<string, TimeSpan>? OperationCompleted;

    /// <summary>
    /// Initialize an in-memory database
    /// </summary>
    public async Task InitializeMemoryDatabaseAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                Dispose(); // Close any existing connection
                
                StatusChanged?.Invoke("Creating in-memory database...");
                _database = TursoDatabase.OpenMemory();
                _connection = _database.Connect();
                ConnectionType = "Memory";
                
                _taskRepository = new TaskEntityRepository(_connection);
                CreateTables();
                StatusChanged?.Invoke("In-memory database ready!");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Error: {ex.Message}");
                throw;
            }
        });
    }

    /// <summary>
    /// Initialize a file-based database
    /// </summary>
    public async Task InitializeFileDatabaseAsync(string path)
    {
        await Task.Run(() =>
        {
            try
            {
                Dispose(); // Close any existing connection
                
                StatusChanged?.Invoke($"Opening database file: {path}");
                _database = TursoDatabase.OpenFile(path);
                _connection = _database.Connect();
                ConnectionType = "File";
                
                CreateTables();
                _taskRepository = new TaskEntityRepository(_connection);
                StatusChanged?.Invoke($"File database ready: {path}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Error: {ex.Message}");
                throw;
            }
        });
    }

    private void CreateTables()
    {
        if (_connection == null) throw new InvalidOperationException("No database connection");

        var sw = Stopwatch.StartNew();

        // Use generated CREATE TABLE statement from TaskEntity
        this._taskRepository?.CreateTable();

        sw.Stop();
        OperationCompleted?.Invoke("Create Tables", sw.Elapsed);
    }

    #region Task Operations

    public async Task<List<TaskItem>> GetAllTasksAsync()
    {
        return await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();

            try
            {
                var entities = _taskRepository.GetAll();
                var tasks = entities.Select(MapEntityToModel).ToList();
                
                sw.Stop();
                OperationCompleted?.Invoke($"Query All Tasks (Count: {tasks.Count})", sw.Elapsed);
                
                return tasks;
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Query All Tasks Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    public async Task<int> AddTaskAsync(TaskItem task)
    {
        return await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var entity = MapModelToEntity(task);
                var id = _taskRepository.Insert(entity);
                
                sw.Stop();
                OperationCompleted?.Invoke("Add Task", sw.Elapsed);
                
                return id;
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Add Task Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    public async Task<int> GetTaskCountAsync()
    {
        return await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var count = _taskRepository.Count();
                
                sw.Stop();
                OperationCompleted?.Invoke("Get Task Count", sw.Elapsed);
                
                return count;
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Get Task Count Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    public async Task<int> GetCompletedTaskCountAsync()
    {
        return await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var count = _taskRepository.CountWhere("is_completed = 1");
                
                sw.Stop();
                OperationCompleted?.Invoke("Get Completed Task Count", sw.Elapsed);
                
                return count;
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Get Completed Task Count Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    public async Task DeleteTaskAsync(int taskId)
    {
        await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                _taskRepository.Delete(taskId);
                
                sw.Stop();
                OperationCompleted?.Invoke("Delete Task", sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Delete Task Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    public async Task UpdateTaskAsync(TaskItem task)
    {
        await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var entity = MapModelToEntity(task);
                _taskRepository.Update(entity);
                
                sw.Stop();
                OperationCompleted?.Invoke("Update Task", sw.Elapsed);
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Update Task Error: {ex.Message}", sw.Elapsed);
                throw;
            }
        });
    }

    #endregion

    #region Performance Testing

    public async Task<TimeSpan> RunPerformanceTestAsync(int operationCount = 1000)
    {
        return await Task.Run(() =>
        {
            if (_taskRepository == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                StatusChanged?.Invoke($"Running performance test with {operationCount} operations...");
                
                for (int i = 0; i < operationCount; i++)
                {
                    var entity = new TaskEntity
                    {
                        Title = $"Test Task {i}",
                        Description = $"Test description for task {i}",
                        IsCompleted = false,
                        CreatedAt = DateTime.Now
                    };
                    _taskRepository.Insert(entity);
                }
                
                sw.Stop();
                var duration = sw.Elapsed;
                
                OperationCompleted?.Invoke($"Performance Test ({operationCount} inserts)", duration);
                StatusChanged?.Invoke($"Performance test completed: {operationCount} operations in {duration.TotalMilliseconds:F2}ms");
                
                return duration;
            }
            catch (Exception ex)
            {
                sw.Stop();
                OperationCompleted?.Invoke($"Performance Test Error: {ex.Message}", sw.Elapsed);
                StatusChanged?.Invoke($"Performance test failed: {ex.Message}");
                throw;
            }
        });
    }

    #endregion

    #region Error Testing

    public async Task TestErrorHandlingAsync()
    {
        await Task.Run(() =>
        {
            if (_connection == null) throw new InvalidOperationException("No database connection");

            try
            {
                StatusChanged?.Invoke("Testing error handling...");
                
                // Try to execute invalid SQL
                _connection.Execute("INVALID SQL STATEMENT");
            }
            catch (TursoException ex)
            {
                StatusChanged?.Invoke($"Expected error caught: {ex.Message}");
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke($"Unexpected error: {ex.Message}");
            }
        });
    }

    #endregion

    #region Mapping Methods

    private TaskItem MapEntityToModel(TaskEntity entity)
    {
        return new TaskItem(
            id: entity.Id,
            title: entity.Title,
            description: entity.Description,
            isCompleted: entity.IsCompleted,
            createdAt: entity.CreatedAt,
            completedAt: entity.CompletedAt
        );
    }

    private TaskEntity MapModelToEntity(TaskItem model)
    {
        return new TaskEntity
        {
            Id = model.Id,
            Title = model.Title,
            Description = model.Description,
            IsCompleted = model.IsCompleted,
            CreatedAt = model.CreatedAt,
            CompletedAt = model.CompletedAt
        };
    }

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _database?.Dispose();
            _connection = null;
            _database = null;
            _taskRepository = null;
            ConnectionType = "None";
            _disposed = true;
        }
    }
}