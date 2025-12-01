using System.Text.Json.Serialization;

namespace S1MCPServer.Models;

/// <summary>
/// Represents a JSON-RPC error response.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code (JSON-RPC standard or custom).
    /// </summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional error data (optional).
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}


