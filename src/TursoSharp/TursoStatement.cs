using System;
using System.Runtime.InteropServices;
using System.Text;
using Turso.Native;

namespace TursoSharp;

/// <summary>
/// Represents a prepared SQL statement
/// </summary>
public sealed unsafe class TursoStatement : IDisposable
{
    private void* _handle;
    private bool _disposed;

    internal TursoStatement(void* handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Execute a step of the statement and return the result
    /// </summary>
    /// <returns>
    /// 1 if a row is available, 0 if done, -1 if error
    /// </returns>
    public int Step()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TursoFFI.turso_statement_step(_handle);
    }

    /// <summary>
    /// Get the number of columns in the result set
    /// </summary>
    public int ColumnCount
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return TursoFFI.turso_statement_column_count(_handle);
        }
    }

    /// <summary>
    /// Get the name of a column by index
    /// </summary>
    public string GetColumnName(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var namePtr = TursoFFI.turso_statement_column_name(_handle, columnIndex);
        if (namePtr == null)
            return string.Empty;

        try
        {
            return Marshal.PtrToStringUTF8((IntPtr)namePtr) ?? string.Empty;
        }
        finally
        {
            TursoFFI.turso_free_string(namePtr);
        }
    }

    /// <summary>
    /// Get the type of a column by index
    /// </summary>
    public TursoColumnType GetColumnType(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var type = TursoFFI.turso_statement_column_type(_handle, columnIndex);
        return (TursoColumnType)type;
    }

    /// <summary>
    /// Get an integer value from a column
    /// </summary>
    public long GetInt64(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TursoFFI.turso_statement_column_int64(_handle, columnIndex);
    }

    /// <summary>
    /// Get a double value from a column
    /// </summary>
    public double GetDouble(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TursoFFI.turso_statement_column_double(_handle, columnIndex);
    }

    /// <summary>
    /// Get a string value from a column
    /// </summary>
    public string? GetString(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var textPtr = TursoFFI.turso_statement_column_text(_handle, columnIndex);
        if (textPtr == null)
            return null;

        try
        {
            return Marshal.PtrToStringUTF8((IntPtr)textPtr);
        }
        finally
        {
            TursoFFI.turso_free_string(textPtr);
        }
    }

    /// <summary>
    /// Check if a column value is NULL
    /// </summary>
    public bool IsNull(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return TursoFFI.turso_statement_column_is_null(_handle, columnIndex);
    }

    /// <summary>
    /// Get a blob value from a column
    /// </summary>
    public byte[]? GetBlob(int columnIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int dataLen;
        var blobPtr = TursoFFI.turso_statement_column_blob(_handle, columnIndex, &dataLen);

        if (blobPtr == null || dataLen <= 0)
            return null;

        try
        {
            var result = new byte[dataLen];
            Marshal.Copy((IntPtr)blobPtr, result, 0, dataLen);
            return result;
        }
        finally
        {
            TursoFFI.turso_free_blob(blobPtr, dataLen);
        }
    }

    /// <summary>
    /// Get a boolean value from a column (from integer)
    /// </summary>
    public bool GetBool(int columnIndex)
    {
        return GetInt64(columnIndex) != 0;
    }

    /// <summary>
    /// Get a byte value from a column
    /// </summary>
    public byte GetByte(int columnIndex)
    {
        return (byte)GetInt64(columnIndex);
    }

    /// <summary>
    /// Get a short value from a column
    /// </summary>
    public short GetInt16(int columnIndex)
    {
        return (short)GetInt64(columnIndex);
    }

    /// <summary>
    /// Get an int value from a column
    /// </summary>
    public int GetInt32(int columnIndex)
    {
        return (int)GetInt64(columnIndex);
    }

    /// <summary>
    /// Get a float value from a column
    /// </summary>
    public float GetFloat(int columnIndex)
    {
        return (float)GetDouble(columnIndex);
    }

    /// <summary>
    /// Bind an integer parameter
    /// </summary>
    public void BindInt64(int parameterIndex, long value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_statement_bind_int64(_handle, parameterIndex, value);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to bind parameter: {errorMessage}");
        }
    }

    /// <summary>
    /// Bind a double parameter
    /// </summary>
    public void BindDouble(int parameterIndex, double value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_statement_bind_double(_handle, parameterIndex, value);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to bind parameter: {errorMessage}");
        }
    }

    /// <summary>
    /// Bind a string parameter
    /// </summary>
    public void BindString(int parameterIndex, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (value == null)
            throw new ArgumentNullException(nameof(value), "String values cannot be null");

        var valueBytes = Encoding.UTF8.GetBytes(value + '\0');
        fixed (byte* valuePtr = valueBytes)
        {
            var result = TursoFFI.turso_statement_bind_text(_handle, parameterIndex, valuePtr);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to bind parameter: {errorMessage}");
            }
        }
    }

    /// <summary>
    /// Bind a blob parameter
    /// </summary>
    public void BindBlob(int parameterIndex, byte[] value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (value == null)
            throw new ArgumentNullException(nameof(value), "Use BindNull() to bind null values");

        if (value.Length == 0)
            throw new ArgumentException("Empty byte arrays are not supported. Use BindNull() for null values.", nameof(value));

        fixed (byte* dataPtr = value)
        {
            var result = TursoFFI.turso_statement_bind_blob(_handle, parameterIndex, dataPtr, value.Length);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to bind parameter: {errorMessage}");
            }
        }
    }

    /// <summary>
    /// Bind a boolean parameter (stored as integer)
    /// </summary>
    public void BindBool(int parameterIndex, bool value)
    {
        BindInt64(parameterIndex, value ? 1 : 0);
    }

    /// <summary>
    /// Bind a byte parameter
    /// </summary>
    public void BindByte(int parameterIndex, byte value)
    {
        BindInt64(parameterIndex, value);
    }

    /// <summary>
    /// Bind a short parameter
    /// </summary>
    public void BindInt16(int parameterIndex, short value)
    {
        BindInt64(parameterIndex, value);
    }

    /// <summary>
    /// Bind an int parameter
    /// </summary>
    public void BindInt32(int parameterIndex, int value)
    {
        BindInt64(parameterIndex, value);
    }

    /// <summary>
    /// Bind a float parameter
    /// </summary>
    public void BindFloat(int parameterIndex, float value)
    {
        BindDouble(parameterIndex, value);
    }

    /// <summary>
    /// Bind a null parameter
    /// </summary>
    public void BindNull(int parameterIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_statement_bind_null(_handle, parameterIndex);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to bind parameter: {errorMessage}");
        }
    }

    /// <summary>
    /// Reset the statement to be executed again
    /// </summary>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = TursoFFI.turso_statement_reset(_handle);
        if (!result.success)
        {
            var errorMessage = GetErrorMessage(result);
            TursoFFI.turso_free_error_message(&result);
            throw new TursoException($"Failed to reset statement: {errorMessage}");
        }
    }

    /// <summary>
    /// Dispose the statement and free associated resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _handle != null)
        {
            var result = TursoFFI.turso_statement_finalize(_handle);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to finalize statement: {errorMessage}");
            }
            _handle = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~TursoStatement()
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

/// <summary>
/// Column data types
/// </summary>
public enum TursoColumnType
{
    Null = 0,
    Integer = 1,
    Real = 2,
    Text = 3,
    Blob = 4
}