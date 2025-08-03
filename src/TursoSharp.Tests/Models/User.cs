using TursoSharp.Attributes;

namespace TursoSharp.Tests.Models;

[TursoEntity(TableName = "users")]
public class User
{
    [TursoPrimaryKey]
    public int Id { get; set; }

    [TursoColumn(ColumnName = "user_name")]
    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    [TursoColumn(ColumnName = "created_at")]
    public DateTime CreatedAt { get; set; }

    [TursoColumn(ColumnName = "is_active")]
    public bool IsActive { get; set; } = true;
}