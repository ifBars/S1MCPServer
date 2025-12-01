using System.Collections.Generic;
using S1MCPServer.Core;
using S1MCPServer.Integrations;
using S1MCPServer.Models;
using S1MCPServer.Utils;

namespace S1MCPServer.Handlers;

/// <summary>
/// Handles handshake and system-level commands.
/// </summary>
public class HandshakeCommandHandler : ICommandHandler
{
    private readonly ResponseQueue _responseQueue;
    private readonly CommandRouter _commandRouter;

    public HandshakeCommandHandler(ResponseQueue responseQueue, CommandRouter commandRouter)
    {
        _responseQueue = responseQueue;
        _commandRouter = commandRouter;
    }

    public void Handle(Request request)
    {
        switch (request.Method)
        {
            case "handshake":
            case "list_methods":
                HandleHandshake(request);
                break;
            case "heartbeat":
                HandleHeartbeat(request);
                break;
            default:
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32601, // Method not found
                    $"Unknown handshake method: {request.Method}"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                break;
        }
    }

    private void HandleHandshake(Request request)
    {
        try
        {
            ModLogger.Debug("Handling handshake request");
            
            var availableMethods = _commandRouter.GetAvailableMethods();
            
            // Categorize methods by type
            var methodCategories = new Dictionary<string, List<string>>
            {
                ["npc"] = new List<string>(),
                ["player"] = new List<string>(),
                ["item"] = new List<string>(),
                ["property"] = new List<string>(),
                ["vehicle"] = new List<string>(),
                ["game_state"] = new List<string>(),
                ["debug"] = new List<string>(),
                ["system"] = new List<string>()
            };

            foreach (var method in availableMethods)
            {
                if (method.StartsWith("get_npc") || method.StartsWith("list_npc") || method.StartsWith("teleport_npc") || method.StartsWith("set_npc"))
                {
                    methodCategories["npc"].Add(method);
                }
                else if (method.StartsWith("get_player") || method.StartsWith("teleport_player") || method.StartsWith("add_item_to_player"))
                {
                    methodCategories["player"].Add(method);
                }
                else if (method.StartsWith("get_item") || method.StartsWith("list_item") || method.StartsWith("spawn_item"))
                {
                    methodCategories["item"].Add(method);
                }
                else if (method.StartsWith("get_property") || method.StartsWith("list_property"))
                {
                    methodCategories["property"].Add(method);
                }
                else if (method.StartsWith("get_vehicle") || method.StartsWith("list_vehicle"))
                {
                    methodCategories["vehicle"].Add(method);
                }
                else if (method.StartsWith("get_game_state"))
                {
                    methodCategories["game_state"].Add(method);
                }
                else if (method.StartsWith("inspect") || method.StartsWith("find_objects") || method.StartsWith("get_scene"))
                {
                    methodCategories["debug"].Add(method);
                }
                else if (method == "handshake" || method == "list_methods")
                {
                    methodCategories["system"].Add(method);
                }
            }

            var result = new Dictionary<string, object>
            {
                ["status"] = "connected",
                ["server_name"] = "S1MCPServer",
                ["version"] = "1.0.0",
                ["total_methods"] = availableMethods.Count,
                ["available_methods"] = availableMethods,
                ["method_categories"] = methodCategories,
                ["integrations"] = new Dictionary<string, bool>
                {
                    ["unityexplorer"] = UnityExplorerIntegration.IsAvailable,
                    ["universe_lib"] = UniverseLibWrapper.IsAvailable
                }
            };

            ModLogger.Debug($"Handshake response: {availableMethods.Count} methods available");
            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleHandshake: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32603, // Internal error
                "Failed to process handshake",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private void HandleHeartbeat(Request request)
    {
        try
        {
            ModLogger.Debug($"Handling heartbeat request (ID: {request.Id})");
            
            // Heartbeat is a simple acknowledgment - just respond with success
            var result = new Dictionary<string, object>
            {
                ["status"] = "alive",
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
            ModLogger.Debug($"Heartbeat response sent (ID: {request.Id})");
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleHeartbeat: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32603, // Internal error
                "Failed to process heartbeat",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }
}

