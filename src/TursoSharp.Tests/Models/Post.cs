using TursoSharp.Attributes;

namespace TursoSharp.Tests.Models;

[TursoEntity(TableName = "posts")]
public class Post
{
    [TursoPrimaryKey]
    public int Id { get; set; }

    [TursoColumn(ColumnName = "user_id")]
    public int UserId { get; set; }

    public string Title { get; set; } = "";

    public string Content { get; set; } = "";

    [TursoColumn(ColumnName = "published_at")]
    public DateTime? PublishedAt { get; set; }

    [TursoColumn(ColumnName = "view_count")]
    public int ViewCount { get; set; } = 0;

    [TursoColumn(ColumnName = "is_published")]
    public bool IsPublished { get; set; } = false;
}