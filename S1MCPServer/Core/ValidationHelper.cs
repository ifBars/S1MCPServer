using System;
using System.Collections.Generic;
using System.Linq;
using S1MCPServer.Helpers;
using S1MCPServer.Models;
using S1MCPServer.Utils;
using UnityEngine;
#if MONO
using ScheduleOne.ItemFramework;
using S1NPC = ScheduleOne.NPCs.NPC;
using S1NPCManager = ScheduleOne.NPCs.NPCManager;
#else
using Il2CppScheduleOne.ItemFramework;
using S1NPC = Il2CppScheduleOne.NPCs.NPC;
using S1NPCManager = Il2CppScheduleOne.NPCs.NPCManager;
#endif

namespace S1MCPServer.Core;

/// <summary>
/// Provides validation utilities for command parameters.
/// </summary>
public static class ValidationHelper
{

    /// <summary>
    /// Validates a position to ensure it's within reasonable bounds.
    /// </summary>
    /// <param name="position">The position to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if position is valid, false otherwise.</returns>
    public static bool ValidatePosition(Vector3 position, out string? errorMessage)
    {
        errorMessage = null;

        // Check if position is underground (y < 0)
        if (position.y < -10.0f)
        {
            errorMessage = "Position is too far underground";
            return false;
        }

        // Check if position is too high (y > 1000)
        if (position.y > 1000.0f)
        {
            errorMessage = "Position is too high";
            return false;
        }

        // Check if position is within reasonable world bounds
        // Adjust these values based on actual game world size
        if (Math.Abs(position.x) > 10000.0f || Math.Abs(position.z) > 10000.0f)
        {
            errorMessage = "Position is outside world bounds";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates a position from a Position object.
    /// </summary>
    /// <param name="position">The position to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if position is valid, false otherwise.</returns>
    public static bool ValidatePosition(Position position, out string? errorMessage)
    {
        if (position == null)
        {
            errorMessage = "Position is null";
            return false;
        }

        return ValidatePosition(position.ToVector3(), out errorMessage);
    }

    /// <summary>
    /// Validates that an NPC ID exists (placeholder - needs custom implementation based on native game classes).
    /// </summary>
    /// <param name="npcId">The NPC ID to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if NPC ID is valid, false otherwise.</returns>
    public static bool ValidateNPCID(string npcId, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(npcId))
        {
            errorMessage = "NPC ID is required";
            return false;
        }

        // TODO: Implement NPC lookup using native game classes (reference S1API NPC management patterns)
        var npc = S1NPCManager.GetNPC(npcId);
        if (npc == null)
        {
            errorMessage = $"NPC '{npcId}' not found";
            return false;
        }
        return true;
    }

    /// <summary>
    /// Validates that an item ID exists in the registry.
    /// </summary>
    /// <param name="itemId">The item ID to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if item ID is valid, false otherwise.</returns>
    public static bool ValidateItemID(string itemId, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(itemId))
        {
            errorMessage = "Item ID is required";
            return false;
        }

        try
        {
            var itemDefinitions = Helpers.Utils.GetAllStorableItemDefinitions();
            var itemExists = itemDefinitions.Any(item => item.ID == itemId);

            if (!itemExists)
            {
                errorMessage = $"Item '{itemId}' not found in registry";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error validating item ID: {ex.Message}");
            errorMessage = "Failed to validate item ID";
            return false;
        }
    }

    /// <summary>
    /// Validates that a property name/ID exists (placeholder - needs custom implementation based on native game classes).
    /// </summary>
    /// <param name="propertyId">The property ID to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if property ID is valid, false otherwise.</returns>
    public static bool ValidatePropertyID(string propertyId, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(propertyId))
        {
            errorMessage = "Property ID is required";
            return false;
        }

        // TODO: Implement property lookup using native game classes (reference S1API Property access patterns)
        // For now, just check that it's not empty
        return true;
    }

    /// <summary>
    /// Validates that a type name exists and can be resolved.
    /// Uses CrossRuntimeTypeHelper for enhanced type resolution.
    /// </summary>
    /// <param name="typeName">The type name to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if type name is valid, false otherwise.</returns>
    public static bool ValidateTypeName(string typeName, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(typeName))
        {
            errorMessage = "Type name is required";
            return false;
        }

        try
        {
            var type = CrossRuntimeTypeHelper.ResolveType(typeName);
            if (type == null)
            {
                errorMessage = $"Type '{typeName}' not found";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error validating type name: {ex.Message}");
            errorMessage = "Failed to validate type name";
            return false;
        }
    }

    /// <summary>
    /// Validates that a component type name exists and is a valid Component type.
    /// Uses CrossRuntimeTypeHelper for enhanced type resolution.
    /// </summary>
    /// <param name="componentTypeName">The component type name to validate.</param>
    /// <param name="errorMessage">Output error message if validation fails.</param>
    /// <returns>True if component type is valid, false otherwise.</returns>
    public static bool ValidateComponentType(string componentTypeName, out string? errorMessage)
    {
        errorMessage = null;

        if (string.IsNullOrEmpty(componentTypeName))
        {
            errorMessage = "Component type name is required";
            return false;
        }

        try
        {
            var type = CrossRuntimeTypeHelper.ResolveType(componentTypeName);
            if (type == null)
            {
                errorMessage = $"Component type '{componentTypeName}' not found";
                return false;
            }

            if (!typeof(Component).IsAssignableFrom(type))
            {
                errorMessage = $"Type '{componentTypeName}' is not a Component";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error validating component type: {ex.Message}");
            errorMessage = "Failed to validate component type";
            return false;
        }
    }

    /// <summary>
    /// Creates an error response for validation failures.
    /// </summary>
    /// <param name="requestId">The request ID.</param>
    /// <param name="errorMessage">The validation error message.</param>
    /// <returns>An error response.</returns>
    public static Response CreateValidationErrorResponse(int requestId, string errorMessage)
    {
        return ProtocolHandler.CreateErrorResponse(
            requestId,
            -32002, // Validation error
            "Parameter validation failed",
            new { reason = errorMessage }
        );
    }
}

