namespace TursoSharp;

/// <summary>
/// Transaction behavior options
/// </summary>
public enum TursoTransactionBehavior
{
    /// <summary>
    /// DEFERRED means that the transaction does not actually start until the database is first accessed
    /// </summary>
    Deferred = 0,

    /// <summary>
    /// IMMEDIATE causes the database connection to start a new write immediately, without waiting for a write statement
    /// </summary>
    Immediate = 1,

    /// <summary>
    /// EXCLUSIVE prevents other database connections from reading the database while the transaction is underway
    /// </summary>
    Exclusive = 2
}