using TursoSharp.Attributes;

namespace TursoSharpExample.Entities;

[TursoEntity(TableName = "tasks")]
public class TaskEntity
{
    [TursoPrimaryKey]
    public int Id { get; set; }

    [TursoColumn(IsNullable = false)]
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    [TursoColumn(ColumnName = "is_completed", DefaultValue = "0")]
    public bool IsCompleted { get; set; }

    [TursoColumn(ColumnName = "created_at", DefaultValue = "CURRENT_TIMESTAMP")]
    public DateTime CreatedAt { get; set; }

    [TursoColumn(ColumnName = "completed_at")]
    public DateTime? CompletedAt { get; set; }
}