using System;
using System.Runtime.InteropServices;
using System.Text;
using Turso.Native;

namespace TursoSharp;

/// <summary>
/// Represents a Turso database instance
/// </summary>
public sealed unsafe class TursoDatabase : IDisposable
{
    private void* _handle;
    private bool _disposed;

    private TursoDatabase(void* handle)
    {
        _handle = handle;
    }

    /// <summary>
    /// Open an in-memory database
    /// </summary>
    /// <returns>A new TursoDatabase instance</returns>
    public static TursoDatabase OpenMemory()
    {
        var handle = TursoFFI.turso_database_open_memory();
        if (handle == null)
        {
            throw new TursoException("Failed to open in-memory database");
        }
        return new TursoDatabase(handle);
    }

    /// <summary>
    /// Open a file-based database
    /// </summary>
    /// <param name="path">Path to the database file</param>
    /// <returns>A new TursoDatabase instance</returns>
    public static TursoDatabase OpenFile(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var pathBytes = Encoding.UTF8.GetBytes(path + '\0');
        fixed (byte* pathPtr = pathBytes)
        {
            var handle = TursoFFI.turso_database_open_file(pathPtr);
            if (handle == null)
            {
                throw new TursoException($"Failed to open database file: {path}");
            }
            return new TursoDatabase(handle);
        }
    }

    /// <summary>
    /// Create a new connection to this database
    /// </summary>
    /// <returns>A new TursoConnection instance</returns>
    public TursoConnection Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var connectionHandle = TursoFFI.turso_connection_open(_handle);
        if (connectionHandle == null)
        {
            throw new TursoException("Failed to create database connection");
        }
        return new TursoConnection(connectionHandle);
    }

    /// <summary>
    /// Dispose the database and free associated resources
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _handle != null)
        {
            var result = TursoFFI.turso_database_close(_handle);
            if (!result.success)
            {
                var errorMessage = GetErrorMessage(result);
                TursoFFI.turso_free_error_message(&result);
                throw new TursoException($"Failed to close database: {errorMessage}");
            }
            _handle = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~TursoDatabase()
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