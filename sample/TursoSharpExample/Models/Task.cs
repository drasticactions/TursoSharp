using CommunityToolkit.Mvvm.ComponentModel;

namespace TursoSharpExample.Models;

public partial class TaskItem : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private DateTime _createdAt;

    [ObservableProperty]
    private DateTime? _completedAt;

    public TaskItem()
    {
        CreatedAt = DateTime.Now;
    }

    public TaskItem(int id, string title, string description, bool isCompleted, DateTime createdAt, DateTime? completedAt = null)
    {
        Id = id;
        Title = title;
        Description = description;
        IsCompleted = isCompleted;
        CreatedAt = createdAt;
        CompletedAt = completedAt;
    }
}