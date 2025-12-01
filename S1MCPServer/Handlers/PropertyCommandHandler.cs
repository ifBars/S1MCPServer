using System.Collections.Generic;
using S1MCPServer.Core;
using S1MCPServer.Models;
using S1MCPServer.Utils;
// TODO: Add property namespace imports when implementing property access
// Native game classes will be accessed via reflection or direct game API
#if MONO
// using ScheduleOne.Properties; // To be determined during implementation
#else
// using Il2CppScheduleOne.Properties; // To be determined during implementation
#endif

namespace S1MCPServer.Handlers;

/// <summary>
/// Handles property-related commands.
/// </summary>
public class PropertyCommandHandler : ICommandHandler
{
    private readonly ResponseQueue _responseQueue;

    public PropertyCommandHandler(ResponseQueue responseQueue)
    {
        _responseQueue = responseQueue;
    }

    public void Handle(Request request)
    {
        switch (request.Method)
        {
            case "list_properties":
                HandleListProperties(request);
                break;
            case "get_property":
                HandleGetProperty(request);
                break;
            default:
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32601, // Method not found
                    $"Unknown property method: {request.Method}"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                break;
        }
    }

    private void HandleListProperties(Request request)
    {
        try
        {
            ModLogger.Debug("Listing properties");

            // TODO: Implement property enumeration using native game classes (reference S1API Property.GetAll() pattern)
            // For now, return empty list
            var result = new Dictionary<string, object>
            {
                ["properties"] = new List<object>(),
                ["count"] = 0
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleListProperties: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to list properties",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private void HandleGetProperty(Request request)
    {
        try
        {
            string? propertyId = null;
            string? propertyName = null;

            if (request.Params != null)
            {
                if (request.Params.TryGetValue("property_id", out var propertyIdObj))
                {
                    propertyId = propertyIdObj?.ToString();
                }
                if (request.Params.TryGetValue("property_name", out var propertyNameObj))
                {
                    propertyName = propertyNameObj?.ToString();
                }
            }

            if (string.IsNullOrEmpty(propertyId) && string.IsNullOrEmpty(propertyName))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "property_id or property_name parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            ModLogger.Debug($"Getting property: {propertyId ?? propertyName}");

            // TODO: Implement property lookup using native game classes (reference S1API Property.GetByName() pattern)
            var result = new Dictionary<string, object>
            {
                ["property_id"] = propertyId ?? "unknown",
                ["name"] = propertyName ?? "Unknown Property",
                ["position"] = new { x = 0.0f, y = 0.0f, z = 0.0f },
                ["npcs_inside"] = new List<object>(),
                ["npc_count"] = 0
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetProperty: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get property",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }
}

