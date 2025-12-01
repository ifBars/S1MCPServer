using System.Collections.Concurrent;
using S1MCPServer.Models;

namespace S1MCPServer.Core;

/// <summary>
/// Thread-safe queue for commands received from the named pipe server.
/// Commands are enqueued from background threads and dequeued on the main thread.
/// </summary>
public class CommandQueue
{
    private readonly ConcurrentQueue<Request> _queue = new();

    /// <summary>
    /// Enqueues a command from a background thread (e.g., named pipe server).
    /// </summary>
    /// <param name="request">The request to enqueue.</param>
    public void EnqueueCommand(Request request)
    {
        _queue.Enqueue(request);
    }

    /// <summary>
    /// Attempts to dequeue a command on the main thread.
    /// </summary>
    /// <param name="request">The dequeued request, or null if queue is empty.</param>
    /// <returns>True if a request was dequeued, false if queue is empty.</returns>
    public bool TryDequeue(out Request? request)
    {
        return _queue.TryDequeue(out request);
    }

    /// <summary>
    /// Gets the current number of queued commands.
    /// </summary>
    public int Count => _queue.Count;

    /// <summary>
    /// Clears all queued commands.
    /// </summary>
    public void Clear()
    {
        while (_queue.TryDequeue(out _)) { }
    }
}


