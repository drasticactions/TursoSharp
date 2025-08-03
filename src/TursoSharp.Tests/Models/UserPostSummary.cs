using TursoSharp.Attributes;

namespace TursoSharp.Tests.Models;

/// <summary>
/// Query result that joins users and posts to get user information with post statistics
/// </summary>
[TursoQueryResult]
public class UserPostSummary
{
    [TursoQueryColumn(ColumnName = "user_id")]
    public int UserId { get; set; }

    [TursoQueryColumn(ColumnName = "user_name")]
    public string UserName { get; set; } = "";

    [TursoQueryColumn(ColumnName = "user_email")]
    public string UserEmail { get; set; } = "";

    [TursoQueryColumn(ColumnName = "user_created_at")]
    public DateTime UserCreatedAt { get; set; }

    [TursoQueryColumn(ColumnName = "is_active")]
    public bool IsActive { get; set; }

    [TursoQueryColumn(ColumnName = "total_posts")]
    public int TotalPosts { get; set; }

    [TursoQueryColumn(ColumnName = "published_posts")]
    public int PublishedPosts { get; set; }

    [TursoQueryColumn(ColumnName = "total_views")]
    public int TotalViews { get; set; }

    [TursoQueryColumn(ColumnName = "latest_post_date", IsOptional = true)]
    public DateTime? LatestPostDate { get; set; }

    [TursoQueryColumn(ColumnName = "avg_views_per_post", IsOptional = true)]
    public double? AverageViewsPerPost { get; set; }
}

/// <summary>
/// Query result for individual post details with user information
/// </summary>
[TursoQueryResult]
public class PostWithUserDetails
{
    [TursoQueryColumn(ColumnName = "post_id")]
    public int PostId { get; set; }

    [TursoQueryColumn(ColumnName = "post_title")]
    public string PostTitle { get; set; } = "";

    [TursoQueryColumn(ColumnName = "post_content")]
    public string PostContent { get; set; } = "";

    [TursoQueryColumn(ColumnName = "published_at")]
    public DateTime? PublishedAt { get; set; }

    [TursoQueryColumn(ColumnName = "view_count")]
    public int ViewCount { get; set; }

    [TursoQueryColumn(ColumnName = "is_published")]
    public bool IsPublished { get; set; }

    [TursoQueryColumn(ColumnName = "author_id")]
    public int AuthorId { get; set; }

    [TursoQueryColumn(ColumnName = "author_name")]
    public string AuthorName { get; set; } = "";

    [TursoQueryColumn(ColumnName = "author_email")]
    public string AuthorEmail { get; set; } = "";

    [TursoQueryColumn(ColumnName = "author_created_at")]
    public DateTime AuthorCreatedAt { get; set; }
}

/// <summary>
/// Simple result for outer join scenarios where user might not have posts
/// </summary>
[TursoQueryResult(GenerateAsync = false)]
public class UserWithOptionalPostInfo
{
    [TursoQueryColumn(ColumnName = "user_id")]
    public int UserId { get; set; }

    [TursoQueryColumn(ColumnName = "user_name")]
    public string UserName { get; set; } = "";

    [TursoQueryColumn(ColumnName = "user_email")]
    public string UserEmail { get; set; } = "";

    // These might be NULL in a LEFT JOIN when user has no posts
    [TursoQueryColumn(ColumnName = "latest_post_title", IsOptional = true)]
    public string? LatestPostTitle { get; set; }

    [TursoQueryColumn(ColumnName = "latest_post_date", IsOptional = true)]
    public DateTime? LatestPostDate { get; set; }

    [TursoQueryColumn(ColumnName = "post_count", IsOptional = true)]
    public int? PostCount { get; set; }
}