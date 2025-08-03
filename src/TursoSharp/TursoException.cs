using System;

namespace TursoSharp;

/// <summary>
/// Exception thrown by Turso operations
/// </summary>
public class TursoException : Exception
{
    public TursoException(string message) : base(message)
    {
    }

    public TursoException(string message, Exception innerException) : base(message, innerException)
    {
    }
}