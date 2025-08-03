namespace TursoSharp.Tests;

[TestClass]
public class TursoErrorHandlingTests
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
    public void Execute_WithInvalidSql_ShouldThrowTursoException()
    {
        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection!.Execute("INVALID SQL STATEMENT"));

        Assert.IsTrue(exception.Message.Contains("Failed to execute SQL"));
    }

    [TestMethod]
    public void Execute_WithSyntaxError_ShouldThrowTursoException()
    {
        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection!.Execute("CREATE TABEL users (id INTEGER)"));

        Assert.IsTrue(exception.Message.Contains("Failed to execute SQL"));
    }

    [TestMethod]
    public void Execute_WithConstraintViolation_ShouldThrowTursoException()
    {
        _connection!.Execute("CREATE TABLE users (id INTEGER PRIMARY KEY, email TEXT)");

        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection.Execute("INSERT INTO users (id, email, nonexistent_column) VALUES (1, 'test@example.com', 'value')"));

        Assert.IsTrue(exception.Message.Contains("Failed to execute SQL"));
    }

    [TestMethod]
    public void QueryScalarInt64_WithInvalidSql_ShouldThrowTursoException()
    {
        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection!.QueryScalarInt64("INVALID SQL STATEMENT"));

        Assert.IsTrue(exception.Message.Contains("Failed to query scalar integer"));
    }

    [TestMethod]
    public void QueryScalarInt64_WithNonNumericResult_ShouldThrowTursoException()
    {
        _connection!.Execute("CREATE TABLE test (value TEXT)");
        _connection.Execute("INSERT INTO test (value) VALUES ('not a number')");

        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection.QueryScalarInt64("SELECT value FROM test"));

        Assert.IsTrue(exception.Message.Contains("Failed to query scalar integer"));
    }

    [TestMethod]
    public void QueryScalarString_WithInvalidSql_ShouldReturnNull()
    {
        var result = _connection!.QueryScalarString("INVALID SQL STATEMENT");
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Execute_WithTableDoesNotExist_ShouldThrowTursoException()
    {
        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection!.Execute("INSERT INTO nonexistent_table (id) VALUES (1)"));

        Assert.IsTrue(exception.Message.Contains("Failed to execute SQL"));
    }

    [TestMethod]
    public void QueryScalarInt64_WithTableDoesNotExist_ShouldThrowTursoException()
    {
        var exception = Assert.ThrowsExactly<TursoException>(() =>
            _connection!.QueryScalarInt64("SELECT COUNT(*) FROM nonexistent_table"));

        Assert.IsTrue(exception.Message.Contains("Failed to query scalar integer"));
    }

    [TestMethod]
    public void QueryScalarString_WithUnicodeCharacters_ShouldWork()
    {
        _connection!.Execute("CREATE TABLE test (value TEXT)");
        var unicodeText = "Hello 世界 🌍 Émile Café";
        _connection.Execute($"INSERT INTO test (value) VALUES ('{unicodeText}')");

        var result = _connection.QueryScalarString("SELECT value FROM test");
        Assert.AreEqual(unicodeText, result);
    }

    [TestMethod]
    public void Execute_WithSpecialCharacters_ShouldWork()
    {
        _connection!.Execute("CREATE TABLE test (value TEXT)");

        var specialText = "Special chars: single'quote double\"quote";
        var escapedText = specialText.Replace("'", "''");
        _connection.Execute($"INSERT INTO test (value) VALUES ('{escapedText}')");

        var result = _connection.QueryScalarString("SELECT value FROM test");
        Assert.AreEqual(specialText, result);
    }
}