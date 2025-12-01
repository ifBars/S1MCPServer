using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using S1MCPServer.Core;
using S1MCPServer.Models;
using S1MCPServer.Utils;
using UnityEngine;
#if MONO
using FishNet;
#else
using Il2CppFishNet;
#endif

namespace S1MCPServer.Handlers;

/// <summary>
/// Handles game state-related commands.
/// </summary>
public class GameStateCommandHandler : ICommandHandler
{
    private readonly ResponseQueue _responseQueue;

    public GameStateCommandHandler(ResponseQueue responseQueue)
    {
        _responseQueue = responseQueue;
    }

    public void Handle(Request request)
    {
        switch (request.Method)
        {
            case "get_game_state":
                HandleGetGameState(request);
                break;
            default:
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32601, // Method not found
                    $"Unknown game state method: {request.Method}"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                break;
        }
    }

    private void HandleGetGameState(Request request)
    {
        try
        {
            ModLogger.Debug("Getting game state");

            // Get current scene name
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            // Get network status
            string networkStatus = "unknown";
            try
            {
                var nm = InstanceFinder.NetworkManager;
                if (nm != null)
                {
                    if (nm.IsServer && nm.IsClient)
                        networkStatus = "host";
                    else if (!nm.IsServer && !nm.IsClient)
                        networkStatus = "singleplayer";
                    else if (nm.IsClient && !nm.IsServer)
                        networkStatus = "client";
                    else if (nm.IsServer && !nm.IsClient)
                        networkStatus = "server";
                }
            }
            catch
            {
                // Network manager not available
            }

            // Get loaded mods
            var loadedMods = MelonMod.RegisteredMelons
                .Select(m => m.Info?.Name ?? "Unknown")
                .ToList();

            // Get game version (from BuildInfo)
            string gameVersion = MelonEnvironment.;

            var result = new Dictionary<string, object>
            {
                ["scene_name"] = sceneName,
                ["game_time"] = 0, // TODO: Get actual game time if available
                ["network_status"] = networkStatus,
                ["game_version"] = gameVersion,
                ["loaded_mods"] = loadedMods
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetGameState: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get game state",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }
}


