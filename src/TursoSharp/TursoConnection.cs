using System;
using System.Runtime.InteropServices;
using System.Text;
using Turso.Native;

namespace TursoSharp;

/// <summary>
/// Represents a connection to a Turso database
/// </summary>
public sealed unsafe class TursoConnection : IDisposable
{
    private void* _handle;
    private bool _disposed;

    internal TursoConnection(void* handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Execute SQL that doesn't return results (INSERT, UPDATE, DELETE, CREATE TABLE, etc.)
    /// </summary>
    /// <param name="sql">The SQL statement to execute</param>
    public void Execute(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        var sqlBytes = Encoding.UTF8.GetBytes(sql + '\0');
        fixed (byte* sqlPtr = sqlBytes)
        {
            ulong rowsChanged = 0;
            var result = TursoFFI.turso_connection_execute(_handle, sqlPtr, &rowsChanged);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to execute SQL: {errorMessage}");
            }
        }
    }

    /// <summary>
    /// Query for a single integer value
    /// </summary>
    /// <param name="sql">The SQL query to execute</param>
    /// <returns>The integer result</returns>
    public long QueryScalarInt64(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        var sqlBytes = Encoding.UTF8.GetBytes(sql + '\0');
        long result = 0;

        fixed (byte* sqlPtr = sqlBytes)
        {
            var queryResult = TursoFFI.turso_connection_query_scalar_int(_handle, sqlPtr, &result);
            if (!queryResult.success)
            {
                var errorMessage = GetErrorMessage(queryResult);
                TursoFFI.turso_free_error_message(&queryResult);
                throw new TursoException($"Failed to query scalar integer: {errorMessage}");
            }
        }

        return result;
    }

    /// <summary>
    /// Query for a single integer value
    /// </summary>
    /// <param name="sql">The SQL query to execute</param>
    /// <returns>The integer result</returns>
    public int QueryScalarInt32(string sql)
    {
        var result = QueryScalarInt64(sql);
        if (result > int.MaxValue || result < int.MinValue)
        {
            throw new OverflowException($"Value {result} is outside the range of Int32");
        }
        return (int)result;
    }

    /// <summary>
    /// Query for a single string value
    /// </summary>
    /// <param name="sql">The SQL query to execute</param>
    /// <returns>The string result, or null if the result was NULL</returns>
    public string? QueryScalarString(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        var sqlBytes = Encoding.UTF8.GetBytes(sql + '\0');

        fixed (byte* sqlPtr = sqlBytes)
        {
            var resultPtr = TursoFFI.turso_connection_query_scalar_string(_handle, sqlPtr);
            if (resultPtr == null)
            {
                return null;
            }

            try
            {
                var result = Marshal.PtrToStringUTF8((IntPtr)resultPtr);
                return result;
            }
            finally
            {
                TursoFFI.turso_free_string(resultPtr);
            }
        }
    }

    /// <summary>
    /// Prepare a SQL statement for execution
    /// </summary>
    /// <param name="sql">The SQL statement to prepare</param>
    /// <returns>A prepared statement</returns>
    public TursoStatement Prepare(string sql)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        var sqlBytes = Encoding.UTF8.GetBytes(sql + '\0');
        fixed (byte* sqlPtr = sqlBytes)
        {
            var statementHandle = TursoFFI.turso_connection_prepare(_handle, sqlPtr);
            if (statementHandle == null)
            {
                throw new TursoException("Failed to prepare statement");
            }
            return new TursoStatement(statementHandle);
        }
    }

    /// <summary>
    /// Execute a query and return a result set
    /// </summary>
    /// <param name="sql">The SQL query to execute</param>
    /// <returns>A result set containing the query results</returns>
    public TursoResultSet Query(string sql)
    {
        var statement = Prepare(sql);
        return new TursoResultSet(statement);
    }

    /// <summary>
    /// Begin a transaction with the specified behavior
    /// </summary>
    /// <param name="behavior">The transaction behavior</param>
    public void BeginTransaction(TursoTransactionBehavior behavior = TursoTransactionBehavior.Deferred)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_connection_begin_transaction(_handle, (int)behavior);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to begin transaction: {errorMessage}");
        }
    }

    /// <summary>
    /// Commit the current transaction
    /// </summary>
    public void CommitTransaction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_connection_commit_transaction(_handle);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to commit transaction: {errorMessage}");
        }
    }

    /// <summary>
    /// Rollback the current transaction
    /// </summary>
    public void RollbackTransaction()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_connection_rollback_transaction(_handle);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to rollback transaction: {errorMessage}");
        }
    }

    /// <summary>
    /// Check if the connection is in autocommit mode
    /// </summary>
    /// <returns>True if in autocommit mode, false if in a transaction</returns>
    public bool IsAutoCommit
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            bool isAutoCommit = false;
            var result = TursoFFI.turso_connection_is_autocommit(_handle, &isAutoCommit);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to check autocommit status: {errorMessage}");
            }
            return isAutoCommit;
        }
    }

    /// <summary>
    /// Dispose the connection and free associated resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _handle != null)
        {
            var result = TursoFFI.turso_connection_close(_handle);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to close connection: {errorMessage}");
            }
            _handle = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~TursoConnection()
    {
        Dispose();
    }

    private static string GetErrorMessage(TursoFFIResult result)
    {
        if (result.error_message == null)
            return "Unknown error";

        try
        {
            return Marshal.PtrToStringUTF8((IntPtr)result.error_message) ?? "Unknown error";
        }
        catch
        {
            return "Unknown error";
        }
    }
}