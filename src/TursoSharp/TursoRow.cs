using System;

namespace TursoSharp;

/// <summary>
/// Represents a row in a query result set
/// </summary>
public sealed class TursoRow
{
    private readonly TursoStatement _statement;

    public TursoRow(TursoStatement statement)
    {
        _statement = statement;
    }

    /// <summary>
    /// Get the number of columns in this row
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
    /// Check if a column value is NULL
    /// </summary>
    public bool IsNull(int columnIndex) => _statement.IsNull(columnIndex);

    /// <summary>
    /// Get an integer value from a column
    /// </summary>
    public long GetInt64(int columnIndex) => _statement.GetInt64(columnIndex);

    /// <summary>
    /// Get an integer value from a column
    /// </summary>
    public int GetInt32(int columnIndex) => (int)_statement.GetInt64(columnIndex);

    /// <summary>
    /// Get a double value from a column
    /// </summary>
    public double GetDouble(int columnIndex) => _statement.GetDouble(columnIndex);

    /// <summary>
    /// Get a float value from a column
    /// </summary>
    public float GetFloat(int columnIndex) => (float)_statement.GetDouble(columnIndex);

    /// <summary>
    /// Get a string value from a column
    /// </summary>
    public string? GetString(int columnIndex) => _statement.GetString(columnIndex);

    /// <summary>
    /// Get a boolean value from a column (stored as integer)
    /// </summary>
    public bool GetBoolean(int columnIndex) => _statement.GetInt64(columnIndex) != 0;

    /// <summary>
    /// Get a DateTime value from a column (stored as text in ISO format)
    /// </summary>
    public DateTime? GetDateTime(int columnIndex)
    {
        var value = _statement.GetString(columnIndex);
        if (value == null) return null;

        if (DateTime.TryParse(value, out var dateTime))
            return dateTime;

        return null;
    }

    /// <summary>
    /// Get a value as an object with automatic type conversion
    /// </summary>
    public object? GetValue(int columnIndex)
    {
        if (IsNull(columnIndex))
            return null;

        return GetColumnType(columnIndex) switch
        {
            TursoColumnType.Integer => GetInt64(columnIndex),
            TursoColumnType.Real => GetDouble(columnIndex),
            TursoColumnType.Text => GetString(columnIndex),
            TursoColumnType.Null => null,
            _ => GetString(columnIndex)
        };
    }

    /// <summary>
    /// Get a value by column name
    /// </summary>
    public object? GetValue(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetValue(columnIndex);
    }

    /// <summary>
    /// Get a string value by column name
    /// </summary>
    public string? GetString(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetString(columnIndex);
    }

    /// <summary>
    /// Get an integer value by column name
    /// </summary>
    public long GetInt64(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetInt64(columnIndex);
    }

    /// <summary>
    /// Get an integer value by column name
    /// </summary>
    public int GetInt32(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetInt32(columnIndex);
    }

    /// <summary>
    /// Get a double value by column name
    /// </summary>
    public double GetDouble(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetDouble(columnIndex);
    }

    /// <summary>
    /// Get a boolean value by column name
    /// </summary>
    public bool GetBoolean(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetBoolean(columnIndex);
    }

    /// <summary>
    /// Get a DateTime value by column name
    /// </summary>
    public DateTime? GetDateTime(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetDateTime(columnIndex);
    }

    /// <summary>
    /// Check if a column value is NULL by column name
    /// </summary>
    public bool IsNull(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return IsNull(columnIndex);
    }

    private int GetColumnIndex(string columnName)
    {
        for (int i = 0; i < ColumnCount; i++)
        {
            if (string.Equals(GetColumnName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        throw new ArgumentException($"Column '{columnName}' not found", nameof(columnName));
    }

    /// <summary>
    /// Indexer to get values by column index
    /// </summary>
    public object? this[int columnIndex] => GetValue(columnIndex);

    /// <summary>
    /// Indexer to get values by column name
    /// </summary>
    public object? this[string columnName] => GetValue(columnName);

    #region Nullable helper methods

    /// <summary>
    /// Get a nullable integer value by column name
    /// </summary>
    public int? GetInt32Nullable(string columnName)
    {
        if (IsNull(columnName)) return null;
        return GetInt32(columnName);
    }

    /// <summary>
    /// Get a nullable long value by column name
    /// </summary>
    public long? GetInt64Nullable(string columnName)
    {
        if (IsNull(columnName)) return null;
        return GetInt64(columnName);
    }

    /// <summary>
    /// Get a nullable double value by column name
    /// </summary>
    public double? GetDoubleNullable(string columnName)
    {
        if (IsNull(columnName)) return null;
        return GetDouble(columnName);
    }

    /// <summary>
    /// Get a nullable float value by column name
    /// </summary>
    public float? GetFloatNullable(string columnName)
    {
        if (IsNull(columnName)) return null;
        return GetFloat(columnName);
    }

    /// <summary>
    /// Get a nullable boolean value by column name
    /// </summary>
    public bool? GetBooleanNullable(string columnName)
    {
        if (IsNull(columnName)) return null;
        return GetBoolean(columnName);
    }

    /// <summary>
    /// Get a float value by column name
    /// </summary>
    public float GetFloat(string columnName)
    {
        var columnIndex = GetColumnIndex(columnName);
        return GetFloat(columnIndex);
    }

    #endregion
}