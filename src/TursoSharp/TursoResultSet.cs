using System;
using System.Collections;
using System.Collections.Generic;

namespace TursoSharp;

/// <summary>
/// Represents a result set from a SQL query
/// </summary>
public sealed class TursoResultSet : IEnumerable<TursoRow>, IDisposable
{
    private readonly TursoStatement _statement;
    private bool _disposed;
    private bool _hasStarted;

    internal TursoResultSet(TursoStatement statement)
    {
        _statement = statement;
    }

    /// <summary>
    /// Get the number of columns in the result set
    /// </summary>
    public int ColumnCount => _statement.ColumnCount;

    /// <summary>
    /// Get the name of a column by index
    /// </summary>
    public string GetColumnName(int columnIndex) => _statement.GetColumnName(columnIndex);

    /// <summary>
    /// Get the type of a column by index
    /// </summary>
    public TursoColumnType GetColumnType(int columnIndex) => _statement.GetColumnType(columnIndex);

    /// <summary>
    /// Move to the next row in the result set
    /// </summary>
    /// <returns>True if a row is available, false if no more rows</returns>
    public bool Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var result = _statement.Step();
        _hasStarted = true;

        return result switch
        {
            1 => true,   // Row available
            0 => false,  // Done
            _ => throw new TursoException("Error stepping through result set")
        };
    }

    /// <summary>
    /// Get the current row
    /// </summary>
    public TursoRow CurrentRow
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_hasStarted)
                throw new InvalidOperationException("Must call Read() before accessing CurrentRow");
            return new TursoRow(_statement);
        }
    }

    /// <summary>
    /// Get an enumerator for all rows in the result set
    /// </summary>
    public IEnumerator<TursoRow> GetEnumerator()
    {
        while (Read())
        {
            yield return CurrentRow;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Dispose the result set and associated statement
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _statement?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    ~TursoResultSet()
    {
        Dispose();
    }
}