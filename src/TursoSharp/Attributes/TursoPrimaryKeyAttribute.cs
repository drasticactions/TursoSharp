using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Marks a property as the primary key for the entity.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TursoPrimaryKeyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether the primary key is auto-incremented.
        /// </summary>
        public bool AutoIncrement { get; set; } = true;

        /// <summary>
        /// Gets or sets the column name. If not specified, the property name will be used.
        /// </summary>
        public string? ColumnName { get; set; }
    }
}