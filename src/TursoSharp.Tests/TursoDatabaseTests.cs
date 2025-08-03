namespace TursoSharp.Tests;

[TestClass]
public class TursoDatabaseTests
{
    [TestMethod]
    public void OpenMemory_ShouldCreateDatabase()
    {
        using var database = TursoDatabase.OpenMemory();
        Assert.IsNotNull(database);
    }

    [TestMethod]
    public void OpenFile_WithValidPath_ShouldCreateDatabase()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using var database = TursoDatabase.OpenFile(tempFile);
            Assert.IsNotNull(database);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [TestMethod]
    public void OpenFile_WithNullPath_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TursoDatabase.OpenFile(null!));
    }

    [TestMethod]
    public void OpenFile_WithEmptyPath_ShouldThrowArgumentException()
    {
        Assert.ThrowsExactly<ArgumentException>(() => TursoDatabase.OpenFile(string.Empty));
    }

    [TestMethod]
    public void Connect_ShouldCreateConnection()
    {
        using var database = TursoDatabase.OpenMemory();
        using var connection = database.Connect();
        Assert.IsNotNull(connection);
    }

    [TestMethod]
    public void Connect_AfterDispose_ShouldThrowObjectDisposedException()
    {
        var database = TursoDatabase.OpenMemory();
        database.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => database.Connect());
    }

    [TestMethod]
    public void Dispose_ShouldAllowMultipleCalls()
    {
        var database = TursoDatabase.OpenMemory();
        database.Dispose();
        database.Dispose(); // Should not throw
    }

    [TestMethod]
    public void MultipleConnections_ShouldWork()
    {
        using var database = TursoDatabase.OpenMemory();

        using var connection1 = database.Connect();
        using var connection2 = database.Connect();

        Assert.IsNotNull(connection1);
        Assert.IsNotNull(connection2);
    }
}