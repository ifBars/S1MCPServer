namespace S1MCPServer.Models;

/// <summary>
/// Base class for command results.
/// </summary>
public abstract class CommandResult
{
    /// <summary>
    /// Indicates if the operation was successful.
    /// </summary>
    public bool Success { get; set; }
}


