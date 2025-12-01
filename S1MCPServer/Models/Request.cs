using System.Text.Json.Serialization;

namespace S1MCPServer.Models;

/// <summary>
/// Represents a JSON-RPC request from the MCP server.
/// </summary>
public class Request
{
    /// <summary>
    /// Unique request identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// Method name to invoke.
    /// </summary>
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Method parameters as a JSON object.
    /// </summary>
    [JsonPropertyName("params")]
    public Dictionary<string, object>? Params { get; set; }
}


