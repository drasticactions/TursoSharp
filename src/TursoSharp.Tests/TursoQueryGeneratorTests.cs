using TursoSharp.Tests.Models;

namespace TursoSharp.Tests;

[TestClass]
public class TursoQueryGeneratorTests
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;
    private UserRepository? _userRepository;
    private PostRepository? _postRepository;

    [TestInitialize]
    public void Setup()
    {
        _database = TursoDatabase.OpenMemory();
        _connection = _database.Connect();
        
        _userRepository = new UserRepository(_connection, false);
        _postRepository = new PostRepository(_connection, false);
        
        // Create tables using generated repository methods
        _userRepository.CreateTable();
        _postRepository.CreateTable();
        
        // Insert test users using generated repository
        var user1Id = _userRepository.Insert(new User
        {
            Name = "Alice Johnson",
            Email = "alice@example.com",
            CreatedAt = new DateTime(2024, 1, 1),
            IsActive = true
        });
        
        var user2Id = _userRepository.Insert(new User
        {
            Name = "Bob Smith",
            Email = "bob@example.com", 
            CreatedAt = new DateTime(2024, 1, 15),
            IsActive = true
        });
        
        var user3Id = _userRepository.Insert(new User
        {
            Name = "Charlie Brown",
            Email = "charlie@example.com",
            CreatedAt = new DateTime(2024, 2, 1),
            IsActive = false
        });
        
        // Insert test posts using generated repository
        _postRepository.Insert(new Post
        {
            UserId = user1Id,
            Title = "Alice's First Post",
            Content = "This is Alice's first blog post.",
            PublishedAt = new DateTime(2024, 1, 10, 10, 0, 0),
            ViewCount = 150,
            IsPublished = true
        });
        
        _postRepository.Insert(new Post
        {
            UserId = user1Id,
            Title = "Alice's Second Post", 
            Content = "Alice writes another post.",
            PublishedAt = new DateTime(2024, 1, 20, 14, 30, 0),
            ViewCount = 200,
            IsPublished = true
        });
        
        _postRepository.Insert(new Post
        {
            UserId = user1Id,
            Title = "Alice's Draft",
            Content = "This is a draft post.",
            PublishedAt = null,
            ViewCount = 0,
            IsPublished = false
        });
        
        _postRepository.Insert(new Post
        {
            UserId = user2Id,
            Title = "Bob's Only Post",
            Content = "Bob's single blog post.",
            PublishedAt = new DateTime(2024, 1, 25, 9, 15, 0),
            ViewCount = 75,
            IsPublished = true
        });
        
        // Note: Charlie (user3) has no posts
    }

    [TestCleanup]
    public void Cleanup()
    {
        _userRepository?.Dispose();
        _postRepository?.Dispose();
        _connection?.Dispose();
        _database?.Dispose();
    }

    [TestMethod]
    public void UserPostSummary_InnerJoin_AggregatesDataCorrectly()
    {
        // Test INNER JOIN with GROUP BY and aggregations
        var sql = @"
            SELECT 
                u.id as user_id,
                u.user_name,
                u.email as user_email,
                u.created_at as user_created_at,
                u.is_active,
                COUNT(p.id) as total_posts,
                SUM(CASE WHEN p.is_published = 1 THEN 1 ELSE 0 END) as published_posts,
                SUM(p.view_count) as total_views,
                MAX(p.published_at) as latest_post_date,
                CAST(AVG(p.view_count) AS REAL) as avg_views_per_post
            FROM users u
            INNER JOIN posts p ON u.id = p.user_id
            GROUP BY u.id, u.user_name, u.email, u.created_at, u.is_active
            ORDER BY u.id";
        
        var results = _connection!.QueryUserPostSummary(sql);
        
        Assert.AreEqual(2, results.Count, "Should only return users who have posts (INNER JOIN)");
        
        // Verify Alice's data
        var alice = results[0];
        Assert.AreEqual(1, alice.UserId);
        Assert.AreEqual("Alice Johnson", alice.UserName);
        Assert.AreEqual("alice@example.com", alice.UserEmail);
        Assert.AreEqual(new DateTime(2024, 1, 1), alice.UserCreatedAt);
        Assert.IsTrue(alice.IsActive);
        Assert.AreEqual(3, alice.TotalPosts);
        Assert.AreEqual(2, alice.PublishedPosts);
        Assert.AreEqual(350, alice.TotalViews); // 150 + 200 + 0
        Assert.AreEqual(new DateTime(2024, 1, 20, 14, 30, 0), alice.LatestPostDate);
        Assert.AreEqual(116.67, alice.AverageViewsPerPost!.Value, 0.01); // (150 + 200 + 0) / 3
        
        // Verify Bob's data
        var bob = results[1];
        Assert.AreEqual(2, bob.UserId);
        Assert.AreEqual("Bob Smith", bob.UserName);
        Assert.AreEqual("bob@example.com", bob.UserEmail);
        Assert.AreEqual(1, bob.TotalPosts);
        Assert.AreEqual(1, bob.PublishedPosts);
        Assert.AreEqual(75, bob.TotalViews);
        Assert.AreEqual(new DateTime(2024, 1, 25, 9, 15, 0), bob.LatestPostDate);
        Assert.AreEqual(75.0, bob.AverageViewsPerPost!.Value);
    }

    [TestMethod]
    public void PostWithUserDetails_InnerJoin_MapsIndividualPostsCorrectly()
    {
        // Test INNER JOIN for individual posts with user details
        var sql = @"
            SELECT 
                p.id as post_id,
                p.title as post_title,
                p.content as post_content,
                p.published_at,
                p.view_count,
                p.is_published,
                u.id as author_id,
                u.user_name as author_name,
                u.email as author_email,
                u.created_at as author_created_at
            FROM posts p
            INNER JOIN users u ON p.user_id = u.id
            WHERE p.is_published = 1
            ORDER BY p.published_at";
        
        var results = _connection!.QueryPostWithUserDetails(sql);
        
        Assert.AreEqual(3, results.Count, "Should return 3 published posts");
        
        // Verify first post (Alice's first)
        var post1 = results[0];
        Assert.AreEqual("Alice's First Post", post1.PostTitle);
        Assert.AreEqual("This is Alice's first blog post.", post1.PostContent);
        Assert.AreEqual(new DateTime(2024, 1, 10, 10, 0, 0), post1.PublishedAt);
        Assert.AreEqual(150, post1.ViewCount);
        Assert.IsTrue(post1.IsPublished);
        Assert.AreEqual(1, post1.AuthorId);
        Assert.AreEqual("Alice Johnson", post1.AuthorName);
        Assert.AreEqual("alice@example.com", post1.AuthorEmail);
        Assert.AreEqual(new DateTime(2024, 1, 1), post1.AuthorCreatedAt);
        
        // Verify Bob's post
        var bobPost = results[2]; // Should be last due to ORDER BY published_at
        Assert.AreEqual("Bob's Only Post", bobPost.PostTitle);
        Assert.AreEqual("Bob Smith", bobPost.AuthorName);
        Assert.AreEqual("bob@example.com", bobPost.AuthorEmail);
    }

    [TestMethod]
    public void UserWithOptionalPostInfo_LeftJoin_HandlesUsersWithoutPosts()
    {
        // Test LEFT JOIN to include users even if they have no posts - simplified query
        var sql = @"
            SELECT 
                u.id as user_id,
                u.user_name,
                u.email as user_email,
                p.title as latest_post_title,
                p.published_at as latest_post_date,
                CASE WHEN p.id IS NOT NULL THEN 1 ELSE NULL END as post_count
            FROM users u
            LEFT JOIN posts p ON u.id = p.user_id AND p.is_published = 1
            ORDER BY u.id, p.published_at DESC";
        
        var results = _connection!.QueryUserWithOptionalPostInfo(sql);
        
        // This query will return multiple rows for users with multiple posts
        // Let's focus on testing that the mapping works correctly
        Assert.IsTrue(results.Count >= 3, "Should return at least 3 results");
        
        // Find Alice - should have posts
        var aliceResults = results.Where(r => r.UserName == "Alice Johnson").ToList();
        Assert.IsTrue(aliceResults.Count > 0, "Alice should have results");
        var firstAlice = aliceResults.First();
        Assert.AreEqual(1, firstAlice.UserId);
        Assert.AreEqual("alice@example.com", firstAlice.UserEmail);
        Assert.IsNotNull(firstAlice.LatestPostTitle, "Alice should have a post title");
        
        // Find Charlie - should have no posts
        var charlieResults = results.Where(r => r.UserName == "Charlie Brown").ToList();
        Assert.AreEqual(1, charlieResults.Count, "Charlie should have exactly one result");
        var charlie = charlieResults.First();
        Assert.AreEqual(3, charlie.UserId);
        Assert.AreEqual("charlie@example.com", charlie.UserEmail);
        Assert.IsNull(charlie.LatestPostTitle, "Charlie should have no post title");
        Assert.IsNull(charlie.LatestPostDate, "Charlie should have no post date");
        Assert.IsNull(charlie.PostCount, "Charlie should have no post count");
    }

    [TestMethod]
    public void PostWithUserDetails_TryMapping_HandlesInvalidData()
    {
        // Test the TryTo method with potentially invalid data
        var sql = @"
            SELECT 
                p.id as post_id,
                p.title as post_title,
                p.content as post_content,
                p.published_at,
                p.view_count,
                p.is_published,
                u.id as author_id,
                u.user_name as author_name,
                u.email as author_email,
                u.created_at as author_created_at
            FROM posts p
            INNER JOIN users u ON p.user_id = u.id
            LIMIT 1";
        
        using var resultSet = _connection!.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());
        
        var success = enumerator.Current.TryToPostWithUserDetails(out var post);
        
        Assert.IsTrue(success);
        Assert.IsNotNull(post);
        Assert.IsFalse(string.IsNullOrEmpty(post.PostTitle));
        Assert.IsFalse(string.IsNullOrEmpty(post.AuthorName));
    }

    [TestMethod]
    public async Task UserPostSummary_AsyncOperations_WorkCorrectly()
    {
        // Test async operations with JOIN queries
        var sql = @"
            SELECT 
                u.id as user_id,
                u.user_name,
                u.email as user_email,
                u.created_at as user_created_at,
                u.is_active,
                COUNT(p.id) as total_posts,
                SUM(CASE WHEN p.is_published = 1 THEN 1 ELSE 0 END) as published_posts,
                SUM(p.view_count) as total_views,
                MAX(p.published_at) as latest_post_date,
                CAST(AVG(p.view_count) AS REAL) as avg_views_per_post
            FROM users u
            LEFT JOIN posts p ON u.id = p.user_id
            GROUP BY u.id, u.user_name, u.email, u.created_at, u.is_active
            ORDER BY u.id";
        
        var results = await _connection!.QueryUserPostSummaryAsync(sql);
        
        Assert.AreEqual(3, results.Count, "Should return all users with LEFT JOIN");
        
        // Verify Charlie (user with no posts) has 0 values
        var charlie = results.First(u => u.UserName == "Charlie Brown");
        Assert.AreEqual(0, charlie.TotalPosts);
        Assert.AreEqual(0, charlie.PublishedPosts);
        Assert.AreEqual(0, charlie.TotalViews);
        // Note: In LEFT JOIN with COUNT, NULL becomes 0, and MAX of empty set returns NULL
        // The exact behavior depends on how the database handles these aggregations
    }

    [TestMethod]
    public void QueryFirstUserPostSummary_ReturnsCorrectUser()
    {
        var sql = @"
            SELECT 
                u.id as user_id,
                u.user_name,
                u.email as user_email,
                u.created_at as user_created_at,
                u.is_active,
                COUNT(p.id) as total_posts,
                SUM(CASE WHEN p.is_published = 1 THEN 1 ELSE 0 END) as published_posts,
                SUM(p.view_count) as total_views,
                MAX(p.published_at) as latest_post_date,
                CAST(AVG(p.view_count) AS REAL) as avg_views_per_post
            FROM users u
            INNER JOIN posts p ON u.id = p.user_id
            WHERE u.user_name = 'Alice Johnson'
            GROUP BY u.id, u.user_name, u.email, u.created_at, u.is_active";
        
        var alice = _connection!.QueryFirstUserPostSummary(sql);
        
        Assert.AreEqual("Alice Johnson", alice.UserName);
        Assert.AreEqual(3, alice.TotalPosts);
        Assert.AreEqual(2, alice.PublishedPosts);
    }

    [TestMethod]
    public void ToPostWithUserDetailsList_MapsResultSetCorrectly()
    {
        var sql = @"
            SELECT 
                p.id as post_id,
                p.title as post_title,
                p.content as post_content,
                p.published_at,
                p.view_count,
                p.is_published,
                u.id as author_id,
                u.user_name as author_name,
                u.email as author_email,
                u.created_at as author_created_at
            FROM posts p
            INNER JOIN users u ON p.user_id = u.id
            ORDER BY p.id";
        
        using var resultSet = _connection!.Query(sql);
        var posts = resultSet.ToPostWithUserDetailsList();
        
        Assert.AreEqual(4, posts.Count); // All posts
        
        // Verify authors are correctly mapped
        var alicePosts = posts.Where(p => p.AuthorName == "Alice Johnson").ToList();
        var bobPosts = posts.Where(p => p.AuthorName == "Bob Smith").ToList();
        
        Assert.AreEqual(3, alicePosts.Count);
        Assert.AreEqual(1, bobPosts.Count);
        
        // Verify all Alice's posts have the same author info
        foreach (var post in alicePosts)
        {
            Assert.AreEqual(1, post.AuthorId);
            Assert.AreEqual("alice@example.com", post.AuthorEmail);
        }
    }

    [TestMethod]
    public void ComplexJoinWithHaving_WorksCorrectly()
    {
        // Complex query with HAVING clause
        var sql = @"
            SELECT 
                u.id as user_id,
                u.user_name,
                u.email as user_email,
                u.created_at as user_created_at,
                u.is_active,
                COUNT(p.id) as total_posts,
                SUM(CASE WHEN p.is_published = 1 THEN 1 ELSE 0 END) as published_posts,
                SUM(p.view_count) as total_views,
                MAX(p.published_at) as latest_post_date,
                CAST(AVG(p.view_count) AS REAL) as avg_views_per_post
            FROM users u
            INNER JOIN posts p ON u.id = p.user_id
            GROUP BY u.id, u.user_name, u.email, u.created_at, u.is_active
            HAVING COUNT(p.id) > 1  -- Only users with more than 1 post
            ORDER BY total_views DESC";
        
        var results = _connection!.QueryUserPostSummary(sql);
        
        Assert.AreEqual(1, results.Count, "Should only return Alice (has 3 posts)");
        
        var alice = results[0];
        Assert.AreEqual("Alice Johnson", alice.UserName);
        Assert.AreEqual(3, alice.TotalPosts);
        Assert.AreEqual(350, alice.TotalViews);
    }

    [TestMethod]
    public void CountWhere_WithActiveUsers_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("is_active = 1");
        
        Assert.AreEqual(2, count, "Should return 2 active users");
    }

    [TestMethod]
    public void CountWhere_WithInactiveUsers_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("is_active = 0");
        
        Assert.AreEqual(1, count, "Should return 1 inactive user (Charlie)");
    }

    [TestMethod]
    public void CountWhere_WithSpecificName_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("user_name = 'Alice Johnson'");
        
        Assert.AreEqual(1, count, "Should return 1 user with name Alice Johnson");
    }

    [TestMethod]
    public void CountWhere_WithEmailPattern_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("email LIKE '%@example.com'");
        
        Assert.AreEqual(3, count, "Should return 3 users with example.com email");
    }

    [TestMethod]
    public void CountWhere_WithDateRange_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("created_at >= '2024-02-01'");
        
        Assert.AreEqual(1, count, "Should return 1 user created after Feb 1, 2024 (Charlie)");
    }

    [TestMethod]
    public void CountWhere_WithComplexCondition_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("is_active = 1 AND created_at < '2024-02-01'");
        
        Assert.AreEqual(2, count, "Should return 2 active users created before Feb 1, 2024");
    }

    [TestMethod]
    public void CountWhere_WithNoMatches_ReturnsZero()
    {
        var count = _userRepository!.CountWhere("user_name = 'NonExistentUser'");
        
        Assert.AreEqual(0, count, "Should return 0 for non-existent user");
    }

    [TestMethod]
    public void CountWhere_WithNullOrEmptyWhereClause_ThrowsArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _userRepository!.CountWhere(""));
        Assert.ThrowsExactly<ArgumentException>(() => _userRepository!.CountWhere("   "));
        Assert.ThrowsExactly<ArgumentException>(() => _userRepository!.CountWhere(null!));
    }

    [TestMethod]
    public async Task CountWhereAsync_WithActiveUsers_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("is_active = 1");
        
        Assert.AreEqual(2, count, "Should return 2 active users");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithInactiveUsers_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("is_active = 0");
        
        Assert.AreEqual(1, count, "Should return 1 inactive user (Charlie)");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithSpecificName_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("user_name = 'Bob Smith'");
        
        Assert.AreEqual(1, count, "Should return 1 user with name Bob Smith");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithEmailPattern_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("email LIKE '%bob%'");
        
        Assert.AreEqual(1, count, "Should return 1 user with 'bob' in email");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithDateRange_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("created_at < '2024-02-01'");
        
        Assert.AreEqual(2, count, "Should return 2 users created before Feb 1, 2024");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithComplexCondition_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("is_active = 0 AND created_at >= '2024-02-01'");
        
        Assert.AreEqual(1, count, "Should return 1 inactive user created after Feb 1, 2024 (Charlie)");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithNoMatches_ReturnsZero()
    {
        var count = await _userRepository!.CountWhereAsync("user_name = 'NonExistentUser'");
        
        Assert.AreEqual(0, count, "Should return 0 for non-existent user");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithNullOrEmptyWhereClause_ThrowsArgumentException()
    {
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _userRepository!.CountWhereAsync(""));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _userRepository!.CountWhereAsync("   "));
        await Assert.ThrowsExactlyAsync<ArgumentException>(() => _userRepository!.CountWhereAsync(null!));
    }

    [TestMethod]
    public void CountWhere_ComparedToRegularCount_IsConsistent()
    {
        var totalCount = _userRepository!.Count();
        var activeCount = _userRepository.CountWhere("is_active = 1");
        var inactiveCount = _userRepository.CountWhere("is_active = 0");
        
        Assert.AreEqual(3, totalCount, "Total count should be 3");
        Assert.AreEqual(totalCount, activeCount + inactiveCount, "Active + inactive should equal total");
    }

    [TestMethod]
    public async Task CountWhereAsync_ComparedToRegularCountAsync_IsConsistent()
    {
        var totalCount = await _userRepository!.CountAsync();
        var activeCount = await _userRepository.CountWhereAsync("is_active = 1");
        var inactiveCount = await _userRepository.CountWhereAsync("is_active = 0");
        
        Assert.AreEqual(3, totalCount, "Total count should be 3");
        Assert.AreEqual(totalCount, activeCount + inactiveCount, "Active + inactive should equal total");
    }

    [TestMethod]
    public void CountWhere_WithIdCondition_ReturnsCorrectCount()
    {
        var count = _userRepository!.CountWhere("id > 2");
        
        Assert.AreEqual(1, count, "Should return 1 user with id > 2 (Charlie)");
    }

    [TestMethod]
    public async Task CountWhereAsync_WithIdCondition_ReturnsCorrectCount()
    {
        var count = await _userRepository!.CountWhereAsync("id <= 2");
        
        Assert.AreEqual(2, count, "Should return 2 users with id <= 2 (Alice and Bob)");
    }
}