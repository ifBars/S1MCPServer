using System.Text.Json.Serialization;
using UnityEngine;

namespace S1MCPServer.Models;

/// <summary>
/// Represents a 3D position for JSON serialization.
/// </summary>
public class Position
{
    /// <summary>
    /// X coordinate.
    /// </summary>
    [JsonPropertyName("x")]
    public float X { get; set; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    [JsonPropertyName("y")]
    public float Y { get; set; }

    /// <summary>
    /// Z coordinate.
    /// </summary>
    [JsonPropertyName("z")]
    public float Z { get; set; }

    /// <summary>
    /// Creates a Position from a Unity Vector3.
    /// </summary>
    public static Position FromVector3(Vector3 vector)
    {
        return new Position
        {
            X = vector.x,
            Y = vector.y,
            Z = vector.z
        };
    }

    /// <summary>
    /// Converts this Position to a Unity Vector3.
    /// </summary>
    public Vector3 ToVector3()
    {
        return new Vector3(X, Y, Z);
    }
}


