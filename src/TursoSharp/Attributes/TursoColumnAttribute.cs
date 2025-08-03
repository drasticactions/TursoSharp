using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Configures how a property is mapped to a database column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TursoColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the column name. If not specified, the property name will be used.
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Gets or sets the SQL data type for the column.
        /// </summary>
        public string? SqlType { get; set; }

        /// <summary>
        /// Gets or sets whether the column allows null values.
        /// </summary>
        public bool IsNullable { get; set; } = true;

        /// <summary>
        /// Gets or sets the default value for the column.
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Gets or sets whether this column should be included in INSERT operations.
        /// </summary>
        public bool IncludeInInsert { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this column should be included in UPDATE operations.
        /// </summary>
        public bool IncludeInUpdate { get; set; } = true;
    }
}