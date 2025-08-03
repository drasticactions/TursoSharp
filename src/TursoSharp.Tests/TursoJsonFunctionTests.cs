namespace TursoSharp.Tests;

[TestClass]
public class TursoJsonFunctionTests
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
    public void JsonFunction_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json('{\"name\": \"test\"}')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("name"));
    }

    [TestMethod]
    public void JsonExtractFunction_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_extract('{\"name\": \"test\", \"age\": 25}', '$.name')");
        Assert.AreEqual("test", result);
    }

    [TestMethod]
    public void JsonArrayFunction_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_array('a', 'b', 'c')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("a"));
        Assert.IsTrue(result.Contains("b"));
        Assert.IsTrue(result.Contains("c"));
    }

    [TestMethod]
    public void JsonObjectFunction_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_object('name', 'John', 'age', 30)");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("John"));
        Assert.IsTrue(result.Contains("30"));
    }

    [TestMethod]
    public void JsonValidFunction_ShouldWork()
    {
        var result = _connection!.QueryScalarInt64("SELECT json_valid('{\"test\": true}')");
        Assert.AreEqual(1, result); // Valid JSON should return 1

        var invalidResult = _connection!.QueryScalarInt64("SELECT json_valid('{invalid json}')");
        Assert.AreEqual(0, invalidResult); // Invalid JSON should return 0
    }

    [TestMethod]
    public void JsonArrowOperator_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT '{\"name\": \"test\"}' -> '$.name'");
        Assert.AreEqual("\"test\"", result); // Arrow operator returns quoted string
    }

    [TestMethod]
    public void JsonDoubleArrowOperator_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT '{\"name\": \"test\"}' ->> '$.name'");
        Assert.AreEqual("test", result); // Double arrow operator returns unquoted string
    }

    [TestMethod]
    public void JsonArrayLength_ShouldWork()
    {
        var result = _connection!.QueryScalarInt64("SELECT json_array_length('[1, 2, 3, 4]')");
        Assert.AreEqual(4, result);
    }

    [TestMethod]
    public void JsonType_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_type('{\"test\": 123}')");
        Assert.AreEqual("object", result);

        var arrayResult = _connection!.QueryScalarString("SELECT json_type('[1, 2, 3]')");
        Assert.AreEqual("array", arrayResult);
    }

    [TestMethod]
    public void JsonSet_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_set('{}', '$.name', 'John')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("John"));
    }

    [TestMethod]
    public void JsonInsert_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_insert('{}', '$.name', 'John')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("John"));
    }

    [TestMethod]
    public void JsonRemove_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_remove('{\"name\": \"John\", \"age\": 30}', '$.age')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("John"));
        Assert.IsFalse(result.Contains("30"));
    }

    [TestMethod]
    public void JsonPatch_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_patch('{\"name\": \"John\"}', '{\"age\": 30}')");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("John"));
        Assert.IsTrue(result.Contains("30"));
    }

    [TestMethod]
    public void JsonQuote_ShouldWork()
    {
        var result = _connection!.QueryScalarString("SELECT json_quote('Hello World')");
        Assert.AreEqual("\"Hello World\"", result);
    }
}