using System;

namespace TursoSharp.Attributes
{
    /// <summary>
    /// Marks a property to be ignored by the TursoSharp source generator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TursoIgnoreAttribute : Attribute
    {
    }
}