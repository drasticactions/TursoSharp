using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Marks a class as a TursoSharp entity that should have database operations generated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class TursoEntityAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the table name. If not specified, the class name will be used.
        /// </summary>
        public string? TableName { get; set; }

        /// <summary>
        /// Gets or sets whether to generate a repository class for this entity.
        /// </summary>
        public bool GenerateRepository { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate async methods.
        /// </summary>
        public bool GenerateAsync { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate the CREATE TABLE statement.
        /// </summary>
        public bool GenerateCreateTable { get; set; } = true;
    }
}