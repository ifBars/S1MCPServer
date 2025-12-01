using System.Collections.Concurrent;
using S1MCPServer.Models;

namespace S1MCPServer.Core;

/// <summary>
/// Thread-safe queue for responses to send back through the named pipe.
/// Responses are enqueued from the main thread and dequeued on background threads.
/// </summary>
public class ResponseQueue
{
    private readonly ConcurrentQueue<Response> _queue = new();

    /// <summary>
    /// Enqueues a response from the main thread (after command execution).
    /// </summary>
    /// <param name="response">The response to enqueue.</param>
    public void EnqueueResponse(Response response)
    {
        _queue.Enqueue(response);
    }

    /// <summary>
    /// Attempts to dequeue a response on a background thread (e.g., named pipe server).
    /// </summary>
    /// <param name="response">The dequeued response, or null if queue is empty.</param>
    /// <returns>True if a response was dequeued, false if queue is empty.</returns>
    public bool TryDequeue(out Response? response)
    {
        return _queue.TryDequeue(out response);
    }

    /// <summary>
    /// Gets the current number of queued responses.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Clears all queued responses.
    /// </summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}


