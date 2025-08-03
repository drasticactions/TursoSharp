namespace TursoSharp.Tests;

[TestClass]
public class TursoStatementValueBindingTests
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
    public void BindBlob_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_blobs (id INTEGER PRIMARY KEY, data BLOB)");
        var testData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F, 0x20, 0x57, 0x6F, 0x72, 0x6C, 0x64 }; // "Hello World"

        using var insertStmt = _connection.Prepare("INSERT INTO test_blobs (data) VALUES (?)");

        // Act
        insertStmt.BindBlob(1, testData);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT data FROM test_blobs WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult); // Row available
        Assert.AreEqual(TursoColumnType.Blob, selectStmt.GetColumnType(0));

        var retrievedData = selectStmt.GetBlob(0);
        Assert.IsNotNull(retrievedData);
        CollectionAssert.AreEqual(testData, retrievedData);
    }

    [TestMethod]
    public void BindBlob_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_blobs (id INTEGER PRIMARY KEY, data BLOB)");

        using var insertStmt = _connection.Prepare("INSERT INTO test_blobs (data) VALUES (?)");

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => insertStmt.BindBlob(1, null!));
    }

    [TestMethod]
    public void BindBlob_WithEmptyArray_ShouldThrowArgumentException()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_blobs (id INTEGER PRIMARY KEY, data BLOB)");
        var emptyData = new byte[0];

        using var insertStmt = _connection.Prepare("INSERT INTO test_blobs (data) VALUES (?)");

        // Act & Assert
        Assert.ThrowsExactly<ArgumentException>(() => insertStmt.BindBlob(1, emptyData));
    }

    [TestMethod]
    public void BindBool_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_bools (id INTEGER PRIMARY KEY, flag BOOLEAN)");

        using var insertStmt = _connection.Prepare("INSERT INTO test_bools (flag) VALUES (?)");

        // Act - Test true
        insertStmt.BindBool(1, true);
        insertStmt.Step();
        insertStmt.Reset();

        // Act - Test false
        insertStmt.BindBool(1, false);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT flag FROM test_bools ORDER BY id");

        // Assert - First row (true)
        var stepResult1 = selectStmt.Step();
        Assert.AreEqual(1, stepResult1);
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(0));
        Assert.AreEqual(1L, selectStmt.GetInt64(0));
        Assert.IsTrue(selectStmt.GetBool(0));

        // Assert - Second row (false)
        var stepResult2 = selectStmt.Step();
        Assert.AreEqual(1, stepResult2);
        Assert.AreEqual(0L, selectStmt.GetInt64(0));
        Assert.IsFalse(selectStmt.GetBool(0));
    }

    [TestMethod]
    public void BindByte_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_bytes (id INTEGER PRIMARY KEY, value INTEGER)");
        const byte testValue = 255;

        using var insertStmt = _connection.Prepare("INSERT INTO test_bytes (value) VALUES (?)");

        // Act
        insertStmt.BindByte(1, testValue);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT value FROM test_bytes WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(0));
        Assert.AreEqual((long)testValue, selectStmt.GetInt64(0));
        Assert.AreEqual(testValue, selectStmt.GetByte(0));
    }

    [TestMethod]
    public void BindInt16_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_int16 (id INTEGER PRIMARY KEY, value INTEGER)");
        const short testValue = 32767;

        using var insertStmt = _connection.Prepare("INSERT INTO test_int16 (value) VALUES (?)");

        // Act
        insertStmt.BindInt16(1, testValue);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT value FROM test_int16 WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(0));
        Assert.AreEqual((long)testValue, selectStmt.GetInt64(0));
        Assert.AreEqual(testValue, selectStmt.GetInt16(0));
    }

    [TestMethod]
    public void BindInt32_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_int32 (id INTEGER PRIMARY KEY, value INTEGER)");
        const int testValue = 2147483647;

        using var insertStmt = _connection.Prepare("INSERT INTO test_int32 (value) VALUES (?)");

        // Act
        insertStmt.BindInt32(1, testValue);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT value FROM test_int32 WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(0));
        Assert.AreEqual((long)testValue, selectStmt.GetInt64(0));
        Assert.AreEqual(testValue, selectStmt.GetInt32(0));
    }

    [TestMethod]
    public void BindFloat_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_float (id INTEGER PRIMARY KEY, value REAL)");
        const float testValue = 3.14159f;

        using var insertStmt = _connection.Prepare("INSERT INTO test_float (value) VALUES (?)");

        // Act
        insertStmt.BindFloat(1, testValue);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT value FROM test_float WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);
        Assert.AreEqual(TursoColumnType.Real, selectStmt.GetColumnType(0));
        Assert.AreEqual((double)testValue, selectStmt.GetDouble(0), 0.000001);
        Assert.AreEqual(testValue, selectStmt.GetFloat(0), 0.000001f);
    }

    [TestMethod]
    public void BindString_AndRetrieve_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_strings (id INTEGER PRIMARY KEY, value TEXT)");
        const string testValue = "Hello, Turso!";

        using var insertStmt = _connection.Prepare("INSERT INTO test_strings (value) VALUES (?)");

        // Act
        insertStmt.BindString(1, testValue);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare("SELECT value FROM test_strings WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);
        Assert.AreEqual(TursoColumnType.Text, selectStmt.GetColumnType(0));
        Assert.AreEqual(testValue, selectStmt.GetString(0));
    }

    [TestMethod]
    public void BindString_WithNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_strings (id INTEGER PRIMARY KEY, value TEXT)");

        using var insertStmt = _connection.Prepare("INSERT INTO test_strings (value) VALUES (?)");

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => insertStmt.BindString(1, null!));
    }

    [TestMethod]
    public void BindMultipleTypes_InSingleQuery_ShouldWorkCorrectly()
    {
        // Arrange
        _connection!.Execute(@"
            CREATE TABLE test_mixed (
                id INTEGER PRIMARY KEY, 
                int_val INTEGER, 
                real_val REAL, 
                text_val TEXT, 
                blob_val BLOB,
                bool_val INTEGER
            )");

        var testBlob = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };

        using var insertStmt = _connection.Prepare(@"
            INSERT INTO test_mixed (int_val, real_val, text_val, blob_val, bool_val) 
            VALUES (?, ?, ?, ?, ?)");

        // Act
        insertStmt.BindInt64(1, 42L);
        insertStmt.BindDouble(2, 3.14159);
        insertStmt.BindString(3, "Mixed types");
        insertStmt.BindBlob(4, testBlob);
        insertStmt.BindBool(5, true);
        insertStmt.Step();

        using var selectStmt = _connection.Prepare(@"
            SELECT int_val, real_val, text_val, blob_val, bool_val 
            FROM test_mixed WHERE id = 1");
        var stepResult = selectStmt.Step();

        // Assert
        Assert.AreEqual(1, stepResult);

        // Check integer
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(0));
        Assert.AreEqual(42L, selectStmt.GetInt64(0));

        // Check real
        Assert.AreEqual(TursoColumnType.Real, selectStmt.GetColumnType(1));
        Assert.AreEqual(3.14159, selectStmt.GetDouble(1), 0.000001);

        // Check text
        Assert.AreEqual(TursoColumnType.Text, selectStmt.GetColumnType(2));
        Assert.AreEqual("Mixed types", selectStmt.GetString(2));

        // Check blob
        Assert.AreEqual(TursoColumnType.Blob, selectStmt.GetColumnType(3));
        var retrievedBlob = selectStmt.GetBlob(3);
        Assert.IsNotNull(retrievedBlob);
        CollectionAssert.AreEqual(testBlob, retrievedBlob);

        // Check boolean
        Assert.AreEqual(TursoColumnType.Integer, selectStmt.GetColumnType(4));
        Assert.IsTrue(selectStmt.GetBool(4));
    }

    [TestMethod]
    public void Statement_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test (id INTEGER PRIMARY KEY, value INTEGER)");
        var stmt = _connection.Prepare("INSERT INTO test (value) VALUES (?)");

        // Act
        stmt.Dispose();

        // Assert
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.BindInt64(1, 42));
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.BindString(1, "test"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.BindBlob(1, new byte[] { 1, 2, 3 }));
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.BindBool(1, true));
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.BindNull(1));
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.Step());
        Assert.ThrowsExactly<ObjectDisposedException>(() => stmt.Reset());
    }

    [TestMethod]
    public void BindNull_ShouldBindNullValueCorrectly()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test_nulls (id INTEGER PRIMARY KEY, name TEXT, value INTEGER)");

        using var insertStmt = _connection.Prepare("INSERT INTO test_nulls (name, value) VALUES (?, ?)");

        // Act
        insertStmt.BindNull(1); // Bind null to name column
        insertStmt.BindNull(2); // Bind null to value column
        var stepResult = insertStmt.Step();

        // Assert
        Assert.AreEqual(0, stepResult, "Statement should complete successfully (return 0 for DONE)");

        // Verify the null values were inserted correctly
        using var selectStmt = _connection.Prepare("SELECT name, value FROM test_nulls WHERE id = 1");
        var queryResult = selectStmt.Step();
        Assert.AreEqual(1, queryResult, "Query should return a row (return 1 for ROW)");

        // Check that the values are indeed null
        Assert.AreEqual(TursoColumnType.Null, selectStmt.GetColumnType(0));
        Assert.AreEqual(TursoColumnType.Null, selectStmt.GetColumnType(1));
        Assert.IsTrue(selectStmt.IsNull(0), "Name column should be null");
        Assert.IsTrue(selectStmt.IsNull(1), "Value column should be null");
    }

    [TestMethod]
    public void BindNull_WithInvalidParameterIndex_ShouldThrowException()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test (id INTEGER)");
        using var stmt = _connection.Prepare("INSERT INTO test (id) VALUES (?)");

        // Act & Assert
        Assert.ThrowsExactly<TursoException>(() => stmt.BindNull(0), "Should throw for parameter index 0");
        Assert.ThrowsExactly<TursoException>(() => stmt.BindNull(-1), "Should throw for negative parameter index");
    }

    [TestMethod]
    public void BindNull_AfterOtherBindings_ShouldOverridePreviousValue()
    {
        // Arrange
        _connection!.Execute("CREATE TABLE test (value TEXT)");
        using var insertStmt = _connection.Prepare("INSERT INTO test (value) VALUES (?)");

        // Act - First bind a string, then bind null
        insertStmt.BindString(1, "test value");
        insertStmt.BindNull(1); // This should override the string binding

        var stepResult = insertStmt.Step();
        Assert.AreEqual(0, stepResult, "Statement should complete successfully");

        // Verify null was stored
        using var selectStmt = _connection.Prepare("SELECT value FROM test");
        var queryResult = selectStmt.Step();
        Assert.AreEqual(1, queryResult, "Query should return a row");
        Assert.AreEqual(TursoColumnType.Null, selectStmt.GetColumnType(0));
        Assert.IsTrue(selectStmt.IsNull(0), "Value should be null");
    }
}