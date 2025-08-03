using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Marks a class as a TursoSharp query result that should have mapping operations generated.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class TursoQueryResultAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets whether to generate extension methods for TursoRow.
        /// </summary>
        public bool GenerateRowExtensions { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate query helper methods.
        /// </summary>
        public bool GenerateQueryHelpers { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to generate async mapping methods.
        /// </summary>
        public bool GenerateAsync { get; set; } = true;
    }
}