using System.Collections.ObjectModel;
using System.Diagnostics;
using TursoSharp;
using TursoSharpExample.Models;

namespace TursoSharpExample.Services;

public class DatabaseService : IDatabaseService
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;
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
        
        // Create tasks table
        _connection.Execute(@"
            CREATE TABLE IF NOT EXISTS tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                description TEXT,
                is_completed INTEGER DEFAULT 0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                completed_at DATETIME
            )");

        sw.Stop();
        OperationCompleted?.Invoke("Create Tables", sw.Elapsed);
    }

    #region Task Operations

    public async Task<List<TaskItem>> GetAllTasksAsync()
    {
        return await Task.Run(() =>
        {
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var tasks = new List<TaskItem>();
            var sw = Stopwatch.StartNew();

            try
            {
                using var resultSet = _connection.Query("SELECT id, title, description, is_completed, created_at, completed_at FROM tasks ORDER BY id");
                
                foreach (var row in resultSet)
                {
                    var task = new TaskItem(
                        id: row.GetInt32("id"),
                        title: row.GetString("title") ?? string.Empty,
                        description: row.GetString("description") ?? string.Empty,
                        isCompleted: row.GetBoolean("is_completed"),
                        createdAt: row.GetDateTime("created_at") ?? DateTime.Now,
                        completedAt: row.GetDateTime("completed_at")
                    );
                    tasks.Add(task);
                }
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                using var statement = _connection.Prepare("INSERT INTO tasks (title, description, is_completed, created_at) VALUES (?, ?, ?, ?)");
                statement.BindString(1, task.Title);
                statement.BindString(2, task.Description);
                statement.BindInt64(3, task.IsCompleted ? 1 : 0);
                statement.BindString(4, task.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                
                var result = statement.Step();
                if (result != 0) // 0 = Done
                {
                    throw new TursoException("Failed to insert task");
                }

                var id = _connection.QueryScalarInt32("SELECT last_insert_rowid()");
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var count = _connection.QueryScalarInt32("SELECT COUNT(*) FROM tasks");
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                var count = _connection.QueryScalarInt32("SELECT COUNT(*) FROM tasks WHERE is_completed = 1");
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                using var statement = _connection.Prepare("DELETE FROM tasks WHERE id = ?");
                statement.BindInt64(1, taskId);
                
                var result = statement.Step();
                if (result != 0) // 0 = Done
                {
                    throw new TursoException("Failed to delete task");
                }
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                using var statement = _connection.Prepare("UPDATE tasks SET title = ?, description = ?, is_completed = ?, completed_at = ? WHERE id = ?");
                statement.BindString(1, task.Title);
                statement.BindString(2, task.Description);
                statement.BindInt64(3, task.IsCompleted ? 1 : 0);
                
                if (task.CompletedAt.HasValue)
                    statement.BindString(4, task.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                else
                    statement.BindNull(4);
                    
                statement.BindInt64(5, task.Id);
                
                var result = statement.Step();
                if (result != 0) // 0 = Done
                {
                    throw new TursoException("Failed to update task");
                }
                
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
            if (_connection == null) throw new InvalidOperationException("No database connection");

            var sw = Stopwatch.StartNew();
            
            try
            {
                StatusChanged?.Invoke($"Running performance test with {operationCount} operations...");
                
                for (int i = 0; i < operationCount; i++)
                {
                    _connection.Execute($"INSERT INTO tasks (title, description) VALUES ('Test Task {i}', 'Test description for task {i}')");
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

    public void Dispose()
    {
        if (!_disposed)
        {
            _connection?.Dispose();
            _database?.Dispose();
            _connection = null;
            _database = null;
            ConnectionType = "None";
            _disposed = true;
        }
    }
}