namespace TursoSharp.Tests;

[TestClass]
public class TursoConnectionTests
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;

    [TestInitialize]
    public void Setup()
    {
        _database = TursoDatabase.OpenMemory();
        _connection = _database.Connect();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
        _database?.Dispose();
    }

    [TestMethod]
    public void Execute_CreateTable_ShouldSucceed()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        // No exception should be thrown
    }

    [TestMethod]
    public void Execute_InsertData_ShouldSucceed()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");
        _connection.Execute("INSERT INTO users (name) VALUES ('Jane Smith')");
        // No exception should be thrown
    }

    [TestMethod]
    public void Execute_UpdateData_ShouldSucceed()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");
        _connection.Execute("UPDATE users SET name = 'John Smith' WHERE id = 1");
        // No exception should be thrown
    }

    [TestMethod]
    public void Execute_DeleteData_ShouldSucceed()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");
        _connection.Execute("DELETE FROM users WHERE id = 1");
        // No exception should be thrown
    }

    [TestMethod]
    public void QueryScalarInt64_Count_ShouldReturnCorrectValue()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");
        _connection.Execute("INSERT INTO users (name) VALUES ('Jane Smith')");

        var count = _connection.QueryScalarInt64("SELECT COUNT(*) FROM users");
        Assert.AreEqual(2L, count);
    }

    [TestMethod]
    public void QueryScalarInt64_Max_ShouldReturnCorrectValue()
    {
        _connection!.Execute("CREATE TABLE numbers (value INTEGER)");
        _connection.Execute("INSERT INTO numbers (value) VALUES (10), (20), (5), (15)");

        var max = _connection.QueryScalarInt64("SELECT MAX(value) FROM numbers");
        Assert.AreEqual(20L, max);
    }

    [TestMethod]
    public void QueryScalarInt32_Count_ShouldReturnCorrectValue()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");

        var count = _connection.QueryScalarInt32("SELECT COUNT(*) FROM users");
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void QueryScalarInt32_WithLargeValue_ShouldThrowOverflowException()
    {
        _connection!.Execute("CREATE TABLE numbers (value INTEGER)");
        _connection.Execute($"INSERT INTO numbers (value) VALUES ({(long)int.MaxValue + 1})");

        Assert.ThrowsExactly<OverflowException>(() =>
            _connection.QueryScalarInt32("SELECT value FROM numbers"));
    }

    [TestMethod]
    public void QueryScalarString_ShouldReturnCorrectValue()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
        _connection.Execute("INSERT INTO users (name) VALUES ('John Doe')");

        var name = _connection.QueryScalarString("SELECT name FROM users WHERE id = 1");
        Assert.AreEqual("John Doe", name);
    }

    [TestMethod]
    public void QueryScalarString_WithNullResult_ShouldReturnNull()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT)");
        _connection.Execute("INSERT INTO users (id, name) VALUES (1, NULL)");

        var name = _connection.QueryScalarString("SELECT name FROM users WHERE id = 1");
        Assert.IsNull(name);
    }

    [TestMethod]
    public void QueryScalarString_WithNoResults_ShouldReturnNull()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");

        var name = _connection.QueryScalarString("SELECT name FROM users WHERE id = 999");
        Assert.IsNull(name);
    }

    [TestMethod]
    public void Execute_WithNullSql_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _connection!.Execute(null!));
    }

    [TestMethod]
    public void Execute_WithEmptySql_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => _connection!.Execute(string.Empty));
    }

    [TestMethod]
    public void QueryScalarInt64_WithNullSql_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _connection!.QueryScalarInt64(null!));
    }

    [TestMethod]
    public void QueryScalarString_WithNullSql_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _connection!.QueryScalarString(null!));
    }

    [TestMethod]
    public void Execute_AfterDispose_ShouldThrowObjectDisposedException()
    {
        _connection!.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _connection.Execute("CREATE TABLE test (id INTEGER)"));
    }

    [TestMethod]
    public void QueryScalarInt64_AfterDispose_ShouldThrowObjectDisposedException()
    {
        _connection!.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _connection.QueryScalarInt64("SELECT 1"));
    }

    [TestMethod]
    public void QueryScalarString_AfterDispose_ShouldThrowObjectDisposedException()
    {
        _connection!.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            _connection.QueryScalarString("SELECT 'test'"));
    }

    [TestMethod]
    public void Dispose_ShouldAllowMultipleCalls()
    {
        _connection!.Dispose();
        _connection.Dispose(); // Should not throw
    }
}