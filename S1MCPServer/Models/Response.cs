using System.Text.Json.Serialization;

namespace S1MCPServer.Models;

/// <summary>
/// Represents a JSON-RPC response to send back to the MCP server.
/// </summary>
public class Response
{
    /// <summary>
    /// Request ID that this response corresponds to.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Result object (null if error occurred).
    /// </summary>
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    /// <summary>
    /// Error object (null if successful).
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ErrorResponse? Error { get; set; }
}


