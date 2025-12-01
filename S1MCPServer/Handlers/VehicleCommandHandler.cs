using System.Collections.Generic;
using S1MCPServer.Core;
using S1MCPServer.Models;
using S1MCPServer.Utils;
#if MONO
using ScheduleOne;
#else
using Il2CppScheduleOne;
#endif

namespace S1MCPServer.Handlers;

/// <summary>
/// Handles vehicle-related commands.
/// </summary>
public class VehicleCommandHandler : ICommandHandler
{
    private readonly ResponseQueue _responseQueue;

    public VehicleCommandHandler(ResponseQueue responseQueue)
    {
        _responseQueue = responseQueue;
    }

    public void Handle(Request request)
    {
        switch (request.Method)
        {
            case "list_vehicles":
                HandleListVehicles(request);
                break;
            case "get_vehicle":
                HandleGetVehicle(request);
                break;
            default:
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32601, // Method not found
                    $"Unknown vehicle method: {request.Method}"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                break;
        }
    }

    private void HandleListVehicles(Request request)
    {
        try
        {
            ModLogger.Debug("Listing vehicles");

            // TODO: Use VehicleManager.Instance.AllVehicles via Utils helper
            // For now, return empty list
            var result = new Dictionary<string, object>
            {
                ["vehicles"] = new List<object>(),
                ["count"] = 0
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleListVehicles: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to list vehicles",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private void HandleGetVehicle(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("vehicle_id", out var vehicleIdObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "vehicle_id parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string vehicleId = vehicleIdObj?.ToString() ?? string.Empty;
            ModLogger.Debug($"Getting vehicle: {vehicleId}");

            // TODO: Implement vehicle lookup
            var result = new Dictionary<string, object>
            {
                ["vehicle_id"] = vehicleId,
                ["vehicle_type"] = "unknown",
                ["position"] = new { x = 0.0f, y = 0.0f, z = 0.0f },
                ["occupants"] = new List<object>()
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetVehicle: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get vehicle",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }
}


