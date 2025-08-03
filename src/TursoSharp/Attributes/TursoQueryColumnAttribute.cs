using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Configures how a property is mapped from a query result column.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TursoQueryColumnAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the column name in the query result. 
        /// If not specified, the property name will be used.
        /// </summary>
        public string? ColumnName { get; set; }

        /// <summary>
        /// Gets or sets whether this column is optional in the query result.
        /// </summary>
        public bool IsOptional { get; set; } = false;

        /// <summary>
        /// Gets or sets a custom converter type for this column.
        /// The type must implement ITursoValueConverter.
        /// </summary>
        public Type? ConverterType { get; set; }

        /// <summary>
        /// Gets or sets the column index if mapping by ordinal position.
        /// </summary>
        public int ColumnIndex { get; set; } = -1;
    }
}