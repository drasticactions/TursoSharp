using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TursoSharp;
using TursoSharpExample.Models;
using TursoSharpExample.Services;

namespace TursoSharpExample.ViewModels;

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly IDatabaseService _databaseService;

    [ObservableProperty]
    private string _greeting = "TursoSharp Todo Backend";

    [ObservableProperty]
    private string _statusMessage = "No database connection";

    [ObservableProperty]
    private string _connectionType = "None";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _taskCount;

    [ObservableProperty]
    private int _completedTaskCount;

    [ObservableProperty]
    private string _newTaskTitle = string.Empty;

    [ObservableProperty]
    private string _newTaskDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _operationLog = new();

    [ObservableProperty]
    private ObservableCollection<TaskItem> _tasks = new();

    [ObservableProperty]
    private TaskItem? _selectedTask;

    [ObservableProperty]
    private bool _isBusy;

    public ICommand InitializeMemoryDatabaseCommand { get; }
    public ICommand InitializeFileDatabaseCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand RefreshStatsCommand { get; }
    public ICommand RefreshDataCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand ToggleTaskCompletionCommand { get; }
    public ICommand RunPerformanceTestCommand { get; }
    public ICommand TestErrorHandlingCommand { get; }
    public ICommand ClearLogCommand { get; }

    public MainViewModel()
    {
        _databaseService = new DatabaseServiceWithGenerators();
        _databaseService.StatusChanged += OnStatusChanged;
        _databaseService.OperationCompleted += OnOperationCompleted;

        InitializeMemoryDatabaseCommand = new AsyncRelayCommand(InitializeMemoryDatabaseAsync, () => !IsBusy);
        InitializeFileDatabaseCommand = new AsyncRelayCommand(InitializeFileDatabaseAsync, () => !IsBusy);
        AddTaskCommand = new AsyncRelayCommand(AddTaskAsync, () => IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(NewTaskTitle));
        RefreshStatsCommand = new AsyncRelayCommand(RefreshStatsAsync, () => IsConnected && !IsBusy);
        RefreshDataCommand = new AsyncRelayCommand(RefreshDataAsync, () => IsConnected && !IsBusy);
        DeleteTaskCommand = new AsyncRelayCommand<TaskItem>(DeleteTaskAsync, task => IsConnected && !IsBusy && task != null);
        ToggleTaskCompletionCommand = new AsyncRelayCommand<TaskItem>(ToggleTaskCompletionAsync, task => IsConnected && !IsBusy && task != null);
        RunPerformanceTestCommand = new AsyncRelayCommand(RunPerformanceTestAsync, () => IsConnected && !IsBusy);
        TestErrorHandlingCommand = new AsyncRelayCommand(TestErrorHandlingAsync, () => IsConnected && !IsBusy);
        ClearLogCommand = new RelayCommand(ClearLog);
        
        // Auto-initialize memory database
        _ = InitializeMemoryDatabaseAsync();
    }

    private async Task InitializeMemoryDatabaseAsync()
    {
        IsBusy = true;
        try
        {
            await _databaseService.InitializeMemoryDatabaseAsync();
            IsConnected = _databaseService.IsConnected;
            ConnectionType = _databaseService.ConnectionType;
            await RefreshStatsAsync();
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to initialize memory database: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InitializeFileDatabaseAsync()
    {
        IsBusy = true;
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TursoSharpExample", "example.db");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            
            await _databaseService.InitializeFileDatabaseAsync(path);
            IsConnected = _databaseService.IsConnected;
            ConnectionType = _databaseService.ConnectionType;
            await RefreshStatsAsync();
            await RefreshDataAsync();
        }
        catch (TursoException ex)
        {
            AddToLog($"Database error: {ex.Message}");
            StatusMessage = $"Error: {ex.Message}";
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to initialize file database: {ex.Message}");
            StatusMessage = $"Failed to open file database: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddTaskAsync()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

        IsBusy = true;
        try
        {
            var task = new TaskItem
            {
                Title = NewTaskTitle,
                Description = NewTaskDescription,
                IsCompleted = false
            };

            var id = await _databaseService.AddTaskAsync(task);
            AddToLog($"Added task '{NewTaskTitle}' with ID {id}");
            
            // Clear form
            NewTaskTitle = string.Empty;
            NewTaskDescription = string.Empty;
            
            await RefreshStatsAsync();
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to add task: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshStatsAsync()
    {
        if (!IsConnected) return;

        try
        {
            TaskCount = await _databaseService.GetTaskCountAsync();
            CompletedTaskCount = await _databaseService.GetCompletedTaskCountAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to refresh stats: {ex.Message}");
        }
    }

    private async Task RunPerformanceTestAsync()
    {
        IsBusy = true;
        try
        {
            var duration = await _databaseService.RunPerformanceTestAsync(100); // Reduced for demo
            AddToLog($"Performance test completed in {duration.TotalMilliseconds:F2}ms");
            await RefreshStatsAsync();
            await RefreshDataAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Performance test failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TestErrorHandlingAsync()
    {
        IsBusy = true;
        try
        {
            await _databaseService.TestErrorHandlingAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Error handling test failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshDataAsync()
    {
        if (!IsConnected) return;

        try
        {
            var tasks = await _databaseService.GetAllTasksAsync();

            Tasks.Clear();
            foreach (var task in tasks)
            {
                Tasks.Add(task);
            }

            AddToLog($"Refreshed data: {tasks.Count} tasks");
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to refresh data: {ex.Message}");
        }
    }

    private async Task DeleteTaskAsync(TaskItem? task)
    {
        if (task == null) return;

        IsBusy = true;
        try
        {
            await _databaseService.DeleteTaskAsync(task.Id);
            Tasks.Remove(task);
            AddToLog($"Deleted task '{task.Title}'");
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to delete task: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ToggleTaskCompletionAsync(TaskItem? task)
    {
        if (task == null) return;

        IsBusy = true;
        try
        {
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.Now : null;
            
            await _databaseService.UpdateTaskAsync(task);
            AddToLog($"Toggled task '{task.Title}' completion to {task.IsCompleted}");
            await RefreshStatsAsync();
        }
        catch (Exception ex)
        {
            AddToLog($"Failed to toggle task completion: {ex.Message}");
            // Revert the change on error
            task.IsCompleted = !task.IsCompleted;
            task.CompletedAt = task.IsCompleted ? DateTime.Now : null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearLog()
    {
        OperationLog.Clear();
    }

    private void OnStatusChanged(string message)
    {
        StatusMessage = message;
        AddToLog(message);
    }

    private void OnOperationCompleted(string operation, TimeSpan duration)
    {
        AddToLog($"{operation}: {duration.TotalMilliseconds:F2}ms");
    }

    private void AddToLog(string message)
    {
        // Ensure we're on the UI thread
        if (OperationLog.Count > 100)
        {
            OperationLog.RemoveAt(0);
        }
        OperationLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
    }

    partial void OnNewTaskTitleChanged(string value)
    {
        ((AsyncRelayCommand)AddTaskCommand).NotifyCanExecuteChanged();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        ((AsyncRelayCommand)AddTaskCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RefreshStatsCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RefreshDataCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand<TaskItem>)DeleteTaskCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand<TaskItem>)ToggleTaskCompletionCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RunPerformanceTestCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)TestErrorHandlingCommand).NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        ((AsyncRelayCommand)InitializeMemoryDatabaseCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)InitializeFileDatabaseCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)AddTaskCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RefreshStatsCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RefreshDataCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand<TaskItem>)DeleteTaskCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand<TaskItem>)ToggleTaskCompletionCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)RunPerformanceTestCommand).NotifyCanExecuteChanged();
        ((AsyncRelayCommand)TestErrorHandlingCommand).NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _databaseService?.Dispose();
    }
}
