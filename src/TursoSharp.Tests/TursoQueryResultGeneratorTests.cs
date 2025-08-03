using TursoSharp.Tests.Models;

namespace TursoSharp.Tests;

[TestClass]
public class TursoQueryResultGeneratorTests
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;

    [TestInitialize]
    public void Setup()
    {
        _database = TursoDatabase.OpenMemory();
        _connection = _database.Connect();

        // Create test table
        _connection.Execute(@"
            CREATE TABLE users (
                id INTEGER PRIMARY KEY,
                user_name TEXT NOT NULL,
                email TEXT NOT NULL,
                last_login DATETIME,
                is_active INTEGER NOT NULL DEFAULT 1,
                score REAL
            )
        ");

        // Insert test data
        _connection.Execute(@"
            INSERT INTO users (user_name, email, last_login, is_active, score) VALUES 
            ('John Doe', 'john@example.com', '2024-01-15 10:30:00', 1, 85.5),
            ('Jane Smith', 'jane@example.com', NULL, 1, 92.0),
            ('Bob Wilson', 'bob@example.com', '2024-01-10 14:20:00', 0, NULL)
        ");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
        _database?.Dispose();
    }

    [TestMethod]
    public void ToUserQueryResult_MapsRowCorrectly()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 1";

        using var resultSet = _connection!.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        var user = enumerator.Current.ToUserQueryResult();

        Assert.AreEqual(1, user.Id);
        Assert.AreEqual("John Doe", user.Name);
        Assert.AreEqual("john@example.com", user.Email);
        Assert.IsNotNull(user.LastLogin);
        Assert.AreEqual(new DateTime(2024, 1, 15, 10, 30, 0), user.LastLogin);
        Assert.IsTrue(user.IsActive);
        Assert.AreEqual(85.5, user.Score);
    }

    [TestMethod]
    public void ToUserQueryResult_HandlesNullValues()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 2";

        using var resultSet = _connection!.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        var user = enumerator.Current.ToUserQueryResult();

        Assert.AreEqual(2, user.Id);
        Assert.AreEqual("Jane Smith", user.Name);
        Assert.AreEqual("jane@example.com", user.Email);
        Assert.IsNull(user.LastLogin);
        Assert.IsTrue(user.IsActive);
        Assert.AreEqual(92.0, user.Score);
    }

    [TestMethod]
    public void ToUserQueryResult_HandlesNullableValues()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 3";

        using var resultSet = _connection!.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        var user = enumerator.Current.ToUserQueryResult();

        Assert.AreEqual(3, user.Id);
        Assert.AreEqual("Bob Wilson", user.Name);
        Assert.AreEqual("bob@example.com", user.Email);
        Assert.IsNotNull(user.LastLogin);
        Assert.IsFalse(user.IsActive);
        Assert.IsNull(user.Score);
    }

    [TestMethod]
    public void TryToUserQueryResult_ReturnsTrueForValidRow()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 1";

        using var resultSet = _connection!.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        var success = enumerator.Current.TryToUserQueryResult(out var user);

        Assert.IsTrue(success);
        Assert.IsNotNull(user);
        Assert.AreEqual(1, user.Id);
        Assert.AreEqual("John Doe", user.Name);
    }

    [TestMethod]
    public void ToUserQueryResultList_MapsAllRows()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users ORDER BY id";

        using var resultSet = _connection!.Query(sql);
        var users = resultSet.ToUserQueryResultList();

        Assert.AreEqual(3, users.Count);

        Assert.AreEqual(1, users[0].Id);
        Assert.AreEqual("John Doe", users[0].Name);

        Assert.AreEqual(2, users[1].Id);
        Assert.AreEqual("Jane Smith", users[1].Name);

        Assert.AreEqual(3, users[2].Id);
        Assert.AreEqual("Bob Wilson", users[2].Name);
    }

    [TestMethod]
    public void QueryUserQueryResult_ReturnsTypedResults()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users ORDER BY id";

        var users = _connection!.QueryUserQueryResult(sql);

        Assert.AreEqual(3, users.Count);
        Assert.AreEqual("John Doe", users[0].Name);
        Assert.AreEqual("Jane Smith", users[1].Name);
        Assert.AreEqual("Bob Wilson", users[2].Name);
    }

    [TestMethod]
    public void QueryFirstUserQueryResult_ReturnsFirstResult()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users ORDER BY id";

        var user = _connection!.QueryFirstUserQueryResult(sql);

        Assert.AreEqual(1, user.Id);
        Assert.AreEqual("John Doe", user.Name);
    }

    [TestMethod]
    public void QueryFirstOrDefaultUserQueryResult_ReturnsFirstResult()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 1";

        var user = _connection!.QueryFirstOrDefaultUserQueryResult(sql);

        Assert.IsNotNull(user);
        Assert.AreEqual(1, user.Id);
        Assert.AreEqual("John Doe", user.Name);
    }

    [TestMethod]
    public void QueryFirstOrDefaultUserQueryResult_ReturnsNullForNoResults()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 999";

        var user = _connection!.QueryFirstOrDefaultUserQueryResult(sql);

        Assert.IsNull(user);
    }

    [TestMethod]
    public void QueryFirstUserQueryResult_ThrowsForNoResults()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users WHERE id = 999";

        Assert.ThrowsExactly<InvalidOperationException>(() => _connection!.QueryFirstUserQueryResult(sql));
    }

    [TestMethod]
    public void SimpleResult_WorksWithoutAsync()
    {
        _connection!.Execute("CREATE TABLE simple (value INTEGER, text TEXT)");
        _connection.Execute("INSERT INTO simple (value, text) VALUES (42, 'test')");

        var sql = "SELECT value, text FROM simple";
        var results = _connection.QuerySimpleResult(sql);

        Assert.AreEqual(1, results.Count);
        Assert.AreEqual(42, results[0].Value);
        Assert.AreEqual("test", results[0].Text);
    }

    [TestMethod]
    public void StructResult_WorksWithStructTypes()
    {
        _connection!.Execute("CREATE TABLE struct_test (id INTEGER, name TEXT)");
        _connection.Execute("INSERT INTO struct_test (id, name) VALUES (1, 'struct test')");

        var sql = "SELECT id, name FROM struct_test";

        using var resultSet = _connection.Query(sql);
        var enumerator = resultSet.GetEnumerator();
        Assert.IsTrue(enumerator.MoveNext());

        var result = enumerator.Current.ToStructResult();

        Assert.AreEqual(1, result.Id);
        Assert.AreEqual("struct test", result.Name);
    }

    [TestMethod]
    public async Task QueryUserQueryResultAsync_WorksAsynchronously()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users ORDER BY id";

        var users = await _connection!.QueryUserQueryResultAsync(sql);

        Assert.AreEqual(3, users.Count);
        Assert.AreEqual("John Doe", users[0].Name);
    }

    [TestMethod]
    public async Task ToUserQueryResultListAsync_WorksAsynchronously()
    {
        var sql = "SELECT id, user_name, email, last_login, is_active, score FROM users ORDER BY id";

        using var resultSet = _connection!.Query(sql);
        var users = await resultSet.ToUserQueryResultListAsync();

        Assert.AreEqual(3, users.Count);
        Assert.AreEqual("John Doe", users[0].Name);
    }
}