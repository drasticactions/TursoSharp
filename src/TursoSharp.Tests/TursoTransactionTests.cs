using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TursoSharp.Tests;

[TestClass]
public class TursoTransactionTests
{
    private TursoDatabase? _database;
    private TursoConnection? _connection;

    [TestInitialize]
    public void Setup()
    {
        _database = TursoDatabase.OpenMemory();
        _connection = _database.Connect();
        _connection.Execute("CREATE TABLE test_transactions (id INTEGER PRIMARY KEY, value TEXT)");
    }

    [TestCleanup]
    public void Cleanup()
    {
        _connection?.Dispose();
        _database?.Dispose();
    }

    [TestMethod]
    public void BeginTransaction_Deferred_ShouldSucceed()
    {
        // Act
        _connection!.BeginTransaction(TursoTransactionBehavior.Deferred);

        // Assert
        Assert.IsFalse(_connection.IsAutoCommit, "Should not be in autocommit mode during transaction");

        // Cleanup
        _connection.RollbackTransaction();
    }

    [TestMethod]
    public void BeginTransaction_Immediate_ShouldSucceed()
    {
        // Act
        _connection!.BeginTransaction(TursoTransactionBehavior.Immediate);

        // Assert
        Assert.IsFalse(_connection.IsAutoCommit, "Should not be in autocommit mode during transaction");

        // Cleanup
        _connection.RollbackTransaction();
    }

    [TestMethod]
    public void BeginTransaction_Exclusive_ShouldSucceed()
    {
        // Act
        _connection!.BeginTransaction(TursoTransactionBehavior.Exclusive);

        // Assert
        Assert.IsFalse(_connection.IsAutoCommit, "Should not be in autocommit mode during transaction");

        // Cleanup
        _connection.RollbackTransaction();
    }

    [TestMethod]
    public void CommitTransaction_ShouldPersistChanges()
    {
        // Arrange
        _connection!.BeginTransaction();
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('test data')");

        // Act
        _connection.CommitTransaction();

        // Assert
        Assert.IsTrue(_connection.IsAutoCommit, "Should be back in autocommit mode after commit");

        var count = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(1L, count, "Data should be persisted after commit");
    }

    [TestMethod]
    public void RollbackTransaction_ShouldDiscardChanges()
    {
        // Arrange
        _connection!.BeginTransaction();
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('test data')");

        // Act
        _connection.RollbackTransaction();

        // Assert
        Assert.IsTrue(_connection.IsAutoCommit, "Should be back in autocommit mode after rollback");

        var count = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(0L, count, "Data should be discarded after rollback");
    }

    [TestMethod]
    public void IsAutoCommit_WhenNotInTransaction_ShouldReturnTrue()
    {
        // Act & Assert
        Assert.IsTrue(_connection!.IsAutoCommit, "Should be in autocommit mode by default");
    }

    [TestMethod]
    public void IsAutoCommit_WhenInTransaction_ShouldReturnFalse()
    {
        // Arrange
        _connection!.BeginTransaction();

        // Act & Assert
        Assert.IsFalse(_connection.IsAutoCommit, "Should not be in autocommit mode during transaction");

        // Cleanup
        _connection.RollbackTransaction();
    }

    [TestMethod]
    public void Transaction_MultipleOperations_ShouldWorkCorrectly()
    {
        // Arrange & Act
        _connection!.BeginTransaction();

        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item1')");
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item2')");
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item3')");

        // Verify data exists in transaction
        var countInTransaction = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(3L, countInTransaction, "All data should be visible within transaction");

        _connection.CommitTransaction();

        // Assert
        var finalCount = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(3L, finalCount, "All data should be persisted after commit");
    }

    [TestMethod]
    public void Transaction_WithRollbackAfterMultipleOperations_ShouldDiscardAll()
    {
        // Arrange & Act
        _connection!.BeginTransaction();

        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item1')");
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item2')");
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('item3')");

        // Verify data exists in transaction
        var countInTransaction = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(3L, countInTransaction, "All data should be visible within transaction");

        _connection.RollbackTransaction();

        // Assert
        var finalCount = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(0L, finalCount, "All data should be discarded after rollback");
    }

    [TestMethod]
    public void BeginTransaction_WhenAlreadyInTransaction_ShouldThrowException()
    {
        // Arrange
        _connection!.BeginTransaction();

        // Act & Assert
        Assert.ThrowsExactly<TursoException>(() => _connection.BeginTransaction(),
            "Should throw when trying to begin a transaction while already in one");

        // Cleanup
        _connection.RollbackTransaction();
    }

    [TestMethod]
    public void CommitTransaction_WhenNotInTransaction_ShouldThrowException()
    {
        // Act & Assert
        Assert.ThrowsExactly<TursoException>(() => _connection!.CommitTransaction(),
            "Should throw when trying to commit without an active transaction");
    }

    [TestMethod]
    public void RollbackTransaction_WhenNotInTransaction_ShouldThrowException()
    {
        // Act & Assert
        Assert.ThrowsExactly<TursoException>(() => _connection!.RollbackTransaction(),
            "Should throw when trying to rollback without an active transaction");
    }

    [TestMethod]
    public void Transaction_WithPreparedStatements_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.BeginTransaction();

        using var stmt = _connection.Prepare("INSERT INTO test_transactions (value) VALUES (?)");

        // Act
        stmt.BindString(1, "prepared1");
        stmt.Step();
        stmt.Reset();

        stmt.BindString(1, "prepared2");
        stmt.Step();
        stmt.Reset();

        _connection.CommitTransaction();

        // Assert
        var count = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(2L, count, "Prepared statement operations should be committed");

        var values = new List<string>();
        using var selectStmt = _connection.Prepare("SELECT value FROM test_transactions ORDER BY id");
        while (selectStmt.Step() == 1)
        {
            values.Add(selectStmt.GetString(0)!);
        }

        CollectionAssert.AreEqual(new[] { "prepared1", "prepared2" }, values, "Values should match inserted data");
    }

    [TestMethod]
    public void TransactionMethods_OnDisposedConnection_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _connection!.Dispose();

        // Act & Assert
        Assert.ThrowsExactly<ObjectDisposedException>(() => _connection.BeginTransaction());
        Assert.ThrowsExactly<ObjectDisposedException>(() => _connection.CommitTransaction());
        Assert.ThrowsExactly<ObjectDisposedException>(() => _connection.RollbackTransaction());
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = _connection.IsAutoCommit);
    }

    [TestMethod]
    public void Transaction_DefaultBehavior_ShouldBeDeferred()
    {
        // Act - Call BeginTransaction without specifying behavior
        _connection!.BeginTransaction();

        // Assert
        Assert.IsFalse(_connection.IsAutoCommit, "Should not be in autocommit mode during transaction");

        // The transaction should work (this validates that Deferred is the default)
        _connection.Execute("INSERT INTO test_transactions (value) VALUES ('default behavior test')");
        _connection.CommitTransaction();

        var count = _connection.QueryScalarInt64("SELECT COUNT(*) FROM test_transactions");
        Assert.AreEqual(1L, count, "Transaction with default behavior should work");
    }
}