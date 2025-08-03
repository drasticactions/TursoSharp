using TursoSharp.Attributes;

namespace TursoSharp.Tests.Models;

[TursoQueryResult]
public class UserQueryResult
{
    public int Id { get; set; }

    [TursoQueryColumn(ColumnName = "user_name")]
    public string Name { get; set; } = "";

    public string Email { get; set; } = "";

    [TursoQueryColumn(IsOptional = true)]
    public DateTime? LastLogin { get; set; }

    public bool IsActive { get; set; }

    public double? Score { get; set; }
}

[TursoQueryResult(GenerateAsync = false)]
public class SimpleResult
{
    public int Value { get; set; }
    public string Text { get; set; } = "";
}

[TursoQueryResult]
public struct StructResult
{
    public int Id { get; set; }
    public string Name { get; set; }
}