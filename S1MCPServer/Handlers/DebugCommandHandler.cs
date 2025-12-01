using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using S1MCPServer.Core;
using S1MCPServer.Integrations;
using S1MCPServer.Models;
using S1MCPServer.Utils;
using UnityEngine;
#if MONO
using ScheduleOne;
using S1NPC = ScheduleOne.NPCs.NPC;
using S1NPCManager = ScheduleOne.NPCs.NPCManager;
#else
using Il2CppScheduleOne;
using S1NPC = Il2CppScheduleOne.NPCs.NPC;
using S1NPCManager = Il2CppScheduleOne.NPCs.NPCManager;
#endif

namespace S1MCPServer.Handlers;

/// <summary>
/// Handles debug and inspection commands.
/// </summary>
public class DebugCommandHandler : ICommandHandler
{
    private readonly ResponseQueue _responseQueue;

    public DebugCommandHandler(ResponseQueue responseQueue)
    {
        _responseQueue = responseQueue;
    }

    public void Handle(Request request)
    {
        switch (request.Method)
        {
            case "inspect_object":
                HandleInspectObject(request);
                break;
            case "inspect_type":
                HandleInspectType(request);
                break;
            case "find_objects_by_type":
                HandleFindObjectsByType(request);
                break;
            case "get_scene_objects":
                HandleGetSceneObjects(request);
                break;
            case "inspect_component":
                HandleInspectComponent(request);
                break;
            case "get_component_by_type":
                HandleGetComponentByType(request);
                break;
            case "get_member_value":
                HandleGetMemberValue(request);
                break;
            default:
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32601, // Method not found
                    $"Unknown debug method: {request.Method}"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                break;
        }
    }

    private void HandleInspectObject(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("object_name", out var objectNameObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "object_name parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string objectName = objectNameObj?.ToString() ?? string.Empty;
            bool useUnityExplorer = false;
            if (request.Params.TryGetValue("use_unityexplorer", out var useUEObj))
            {
                bool.TryParse(useUEObj?.ToString(), out useUnityExplorer);
            }

            ModLogger.Debug($"Inspecting object: {objectName} (useUnityExplorer: {useUnityExplorer})");

            // Find GameObject using enhanced ReflectionHelper (with UniverseLib support)
            var gameObject = ReflectionHelper.FindGameObject(objectName);
            if (gameObject == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "GameObject not found",
                    new { object_name = objectName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            // Use UnityExplorer integration if requested and available
            Dictionary<string, object>? result = null;
            if (useUnityExplorer && UnityExplorerIntegration.IsAvailable)
            {
                result = UnityExplorerIntegration.GetDetailedInspection(gameObject);
            }

            // Fallback to enhanced ReflectionHelper inspection
            if (result == null)
            {
                result = InspectGameObjectWithReflection(gameObject);
            }

            // Add metadata
            result["unityexplorer_available"] = UnityExplorerIntegration.IsAvailable;
            result["universe_lib_available"] = UniverseLibWrapper.IsAvailable;

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleInspectObject: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to inspect object",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private Dictionary<string, object> InspectGameObjectWithReflection(GameObject gameObject)
    {
        var result = new Dictionary<string, object>
        {
            ["object_name"] = gameObject.name,
            ["object_type"] = "GameObject",
            ["full_type"] = gameObject.GetType().FullName ?? "Unknown"
        };

        // Get position and transform info
        var transform = gameObject.transform;
        result["position"] = new { x = transform.position.x, y = transform.position.y, z = transform.position.z };
        result["rotation"] = new { x = transform.rotation.x, y = transform.rotation.y, z = transform.rotation.z, w = transform.rotation.w };
        result["scale"] = new { x = transform.localScale.x, y = transform.localScale.y, z = transform.localScale.z };

        // Get all components with enhanced reflection
        // Unity components can be Component or MonoBehaviour, so we need to get both
        var components = new List<Dictionary<string, object>>();
        var componentSet = new HashSet<Component>(); // Use HashSet to avoid duplicates
        
        try
        {
            // Get all Component types (includes Transform, etc.)
            var allComponents = gameObject.GetComponents<Component>();
            foreach (var component in allComponents)
            {
                if (component != null)
                    componentSet.Add(component);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"Error getting Component types: {ex.Message}");
        }

        try
        {
            // Also get MonoBehaviour components (custom game components)
            var monoBehaviours = gameObject.GetComponents<MonoBehaviour>();
            foreach (var mb in monoBehaviours)
            {
                if (mb != null)
                    componentSet.Add(mb);
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"Error getting MonoBehaviour types: {ex.Message}");
        }

        // Inspect all unique components
        foreach (var component in componentSet)
        {
            if (component == null) continue;

            try
            {
                var componentData = InspectComponent(component);
                components.Add(componentData);
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error inspecting component {component.GetType().Name}: {ex.Message}");
                // Add error info instead
                components.Add(new Dictionary<string, object>
                {
                    ["type"] = component.GetType().Name,
                    ["full_type"] = component.GetType().FullName ?? "Unknown",
                    ["error"] = ex.Message
                });
            }
        }

        result["components"] = components;
        result["component_count"] = components.Count;
        result["component_types"] = components.Select(c => c.ContainsKey("type") ? c["type"]?.ToString() : "Unknown").ToList();

        // Get hierarchy with full paths
        var hierarchy = new List<Dictionary<string, object>>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            hierarchy.Add(new Dictionary<string, object>
            {
                ["name"] = child.name,
                ["path"] = GetGameObjectPath(child.gameObject)
            });
        }
        result["children"] = hierarchy;
        result["child_count"] = hierarchy.Count;

        return result;
    }

    private Dictionary<string, object> InspectComponent(Component component)
    {
        var componentData = new Dictionary<string, object>
        {
            ["type"] = component.GetType().Name,
            ["full_type"] = component.GetType().FullName ?? "Unknown"
        };

        // Get properties using enhanced reflection
        var properties = new Dictionary<string, object>();
        try
        {
            var props = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var prop in props.Take(20)) // Limit to first 20 properties
            {
                try
                {
                    // Skip IntPtr properties (common in IL2CPP, not serializable)
                    if (IsIntPtrType(prop.PropertyType))
                        continue;

                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        var value = prop.GetValue(component);
                        
                        // Skip if the value itself is IntPtr
                        if (value != null && IsIntPtrType(value.GetType()))
                            continue;
                            
                        var formattedValue = FormatValue(value);
                        if (formattedValue != null)
                        {
                            properties[prop.Name] = formattedValue;
                        }
                    }
                }
                catch
                {
                    // Skip properties that can't be read
                }
            }
        }
        catch
        {
            // Failed to get properties
        }
        componentData["properties"] = properties;

        // Get fields using enhanced reflection
        var fields = new Dictionary<string, object>();
        try
        {
            var fieldInfos = component.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var field in fieldInfos.Take(20)) // Limit to first 20 fields
            {
                try
                {
                    // Skip IntPtr fields (common in IL2CPP, not serializable)
                    if (IsIntPtrType(field.FieldType))
                        continue;

                    var value = ReflectionHelper.GetFieldValue(component, field.Name);
                    
                    // Skip if the value itself is IntPtr
                    if (value != null && IsIntPtrType(value.GetType()))
                        continue;
                        
                    var formattedValue = FormatValue(value);
                    if (formattedValue != null)
                    {
                        fields[field.Name] = formattedValue;
                    }
                }
                catch
                {
                    // Skip fields that can't be read
                }
            }
        }
        catch
        {
            // Failed to get fields
        }
        componentData["fields"] = fields;

        return componentData;
    }

    /// <summary>
    /// Checks if a type is IntPtr or UIntPtr (not serializable and can generally be ignored in IL2CPP).
    /// </summary>
    private static bool IsIntPtrType(Type type)
    {
        return type == typeof(IntPtr) || type == typeof(UIntPtr);
    }

    private object? FormatValue(object? value)
    {
        if (value == null)
            return "null";

        var type = value.GetType();
        
        // Skip IntPtr values - they're not serializable
        if (IsIntPtrType(type))
            return null; // Return null to signal this should be skipped

        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return value;

        if (value is UnityEngine.Object unityObj)
            return $"{unityObj.GetType().Name}:{unityObj.name}";

        // For other types, try to convert to string but be safe
        try
        {
            return value.ToString() ?? "null";
        }
        catch
        {
            // If ToString() fails, skip this value
            return null;
        }
    }

    private string GetGameObjectPath(GameObject obj)
    {
        var path = obj.name;
        var current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private void HandleInspectType(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("type_name", out var typeNameObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "type_name parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string typeName = typeNameObj?.ToString() ?? string.Empty;
            ModLogger.Debug($"Inspecting type: {typeName}");

            // Try to find the type
            Type? type = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(typeName);
                    if (type != null)
                        break;
                }
                catch
                {
                    // Continue searching
                }
            }

            if (type == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "Type not found",
                    new { type_name = typeName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            // Try UnityExplorer if available
            bool inspectedViaUnityExplorer = false;
            if (UnityExplorerIntegration.IsAvailable)
            {
                inspectedViaUnityExplorer = UnityExplorerIntegration.InspectType(type);
            }

            // Get type information
            var result = new Dictionary<string, object>
            {
                ["type_name"] = type.Name,
                ["full_type_name"] = type.FullName ?? "Unknown",
                ["namespace"] = type.Namespace ?? "Unknown",
                ["is_class"] = type.IsClass,
                ["is_interface"] = type.IsInterface,
                ["is_enum"] = type.IsEnum,
                ["base_type"] = type.BaseType?.FullName ?? "None",
                ["inspected_via_unityexplorer"] = inspectedViaUnityExplorer,
                ["unityexplorer_available"] = UnityExplorerIntegration.IsAvailable
            };

            // Get members
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Take(50)
                .Select(m => m.Name)
                .ToList();
            result["methods"] = methods;
            result["method_count"] = methods.Count;

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleInspectType: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to inspect type",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private void HandleFindObjectsByType(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("component_type", out var componentTypeObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "component_type parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string componentTypeName = componentTypeObj?.ToString() ?? string.Empty;
            ModLogger.Debug($"Finding objects by type: {componentTypeName}");

            // Try to find the type
            Type? componentType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    componentType = assembly.GetType(componentTypeName);
                    if (componentType != null && typeof(Component).IsAssignableFrom(componentType))
                        break;
                }
                catch
                {
                    // Continue searching
                }
            }

            if (componentType == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "Component type not found or is not a Component",
                    new { component_type = componentTypeName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            // Use ReflectionHelper's enhanced FindGameObjectsByType
            // We need to use reflection to call the generic method
            var method = typeof(ReflectionHelper).GetMethod("FindGameObjectsByType", BindingFlags.Public | BindingFlags.Static);
            List<GameObject>? gameObjectsList = null;
            
            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(componentType);
                gameObjectsList = genericMethod.Invoke(null, null) as List<GameObject>;
            }

            if (gameObjectsList == null)
            {
                gameObjectsList = new List<GameObject>();
            }
            
            var objectResults = gameObjectsList.Select(go => new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["path"] = GetGameObjectPath(go),
                ["position"] = new { x = go.transform.position.x, y = go.transform.position.y, z = go.transform.position.z }
            }).ToList();

            var finalResult = new Dictionary<string, object>
            {
                ["component_type"] = componentTypeName,
                ["objects"] = objectResults,
                ["count"] = objectResults.Count,
                ["universe_lib_available"] = UniverseLibWrapper.IsAvailable
            };

            var finalResponse = ProtocolHandler.CreateSuccessResponse(request.Id, finalResult);
            _responseQueue.EnqueueResponse(finalResponse);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleFindObjectsByType: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to find objects by type",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    private void HandleGetSceneObjects(Request request)
    {
        try
        {
            ModLogger.Debug("Getting scene objects");

            // Use enhanced ReflectionHelper to find all GameObjects
            var allObjects = ReflectionHelper.FindAllGameObjects();
            
            // Group by scene
            var sceneObjects = new Dictionary<string, List<Dictionary<string, object>>>();
            foreach (var obj in allObjects)
            {
                var sceneName = obj.scene.name;
                if (!sceneObjects.ContainsKey(sceneName))
                {
                    sceneObjects[sceneName] = new List<Dictionary<string, object>>();
                }

                sceneObjects[sceneName].Add(new Dictionary<string, object>
                {
                    ["name"] = obj.name,
                    ["path"] = GetGameObjectPath(obj),
                    ["active"] = obj.activeSelf
                });
            }

            var result = new Dictionary<string, object>
            {
                ["scenes"] = sceneObjects.Select(kvp => new Dictionary<string, object>
                {
                    ["scene_name"] = kvp.Key,
                    ["objects"] = kvp.Value,
                    ["count"] = kvp.Value.Count
                }).ToList(),
                ["total_objects"] = allObjects.Count,
                ["universe_lib_available"] = UniverseLibWrapper.IsAvailable
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetSceneObjects: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get scene objects",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    /// <summary>
    /// Universal component inspection - finds and inspects any component by type name.
    /// Supports partial type name matching and can inspect from a specific GameObject or NPC ID.
    /// </summary>
    private void HandleInspectComponent(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("component_type", out var componentTypeObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "component_type parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string componentTypeName = componentTypeObj?.ToString() ?? string.Empty;
            string? objectName = null;
            string? npcId = null;
            
            if (request.Params.TryGetValue("object_name", out var objectNameObj))
            {
                objectName = objectNameObj?.ToString();
            }
            
            if (request.Params.TryGetValue("npc_id", out var npcIdObj))
            {
                npcId = npcIdObj?.ToString();
            }

            int maxDepth = 3;
            if (request.Params.TryGetValue("max_depth", out var maxDepthObj))
            {
                int.TryParse(maxDepthObj?.ToString(), out maxDepth);
            }

            ModLogger.Debug($"Inspecting component: {componentTypeName} on object: {objectName ?? npcId ?? "any"}");

            Component? component = null;
            GameObject? gameObject = null;

            // If npc_id is provided, get GameObject from NPC
            if (!string.IsNullOrEmpty(npcId))
            {
                gameObject = GetGameObjectFromNPCId(npcId);
                if (gameObject == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        "NPC not found or has no GameObject",
                        new { npc_id = npcId }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }
                objectName = gameObject.name; // Use GameObject name for consistency
            }
            // If object_name is provided, find GameObject
            else if (!string.IsNullOrEmpty(objectName))
            {
                gameObject = ReflectionHelper.FindGameObject(objectName);
                if (gameObject == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        "GameObject not found",
                        new { object_name = objectName }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }
            }

            // Find component
            if (gameObject != null)
            {
                component = FindComponentByTypeName(gameObject, componentTypeName);
            }
            else
            {
                // Find first component of this type in the scene
                component = FindComponentByTypeNameInScene(componentTypeName);
            }

            if (component == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "Component not found",
                    new { component_type = componentTypeName, object_name = objectName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            // Inspect the component with enhanced reflection
            var result = InspectComponentDeep(component, maxDepth);
            result["component_type"] = component.GetType().FullName ?? component.GetType().Name;
            result["game_object"] = component.gameObject.name;
            result["game_object_path"] = GetGameObjectPath(component.gameObject);

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleInspectComponent: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to inspect component",
                new { details = ex.Message, stack_trace = ex.StackTrace }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    /// <summary>
    /// Gets a component by type name from a specific GameObject.
    /// </summary>
    private void HandleGetComponentByType(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("object_name", out var objectNameObj) ||
                !request.Params.TryGetValue("component_type", out var componentTypeObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "object_name and component_type parameters are required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string objectName = objectNameObj?.ToString() ?? string.Empty;
            string componentTypeName = componentTypeObj?.ToString() ?? string.Empty;

            var gameObject = ReflectionHelper.FindGameObject(objectName);
            if (gameObject == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "GameObject not found",
                    new { object_name = objectName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            var component = FindComponentByTypeName(gameObject, componentTypeName);
            if (component == null)
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32000, // Game error
                    "Component not found",
                    new { component_type = componentTypeName, object_name = objectName }
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            var result = new Dictionary<string, object>
            {
                ["component_type"] = component.GetType().FullName ?? component.GetType().Name,
                ["component_name"] = component.GetType().Name,
                ["game_object"] = gameObject.name,
                ["found"] = true
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetComponentByType: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get component by type",
                new { details = ex.Message }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    /// <summary>
    /// Gets a member value (property or field) from an object, supporting nested access (e.g., "Dealer.Home.BuildingName").
    /// </summary>
    private void HandleGetMemberValue(Request request)
    {
        try
        {
            if (request.Params == null || !request.Params.TryGetValue("object_name", out var objectNameObj) ||
                !request.Params.TryGetValue("member_path", out var memberPathObj))
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "object_name and member_path parameters are required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            string objectName = objectNameObj?.ToString() ?? string.Empty;
            string memberPath = memberPathObj?.ToString() ?? string.Empty;
            string? componentType = null;
            string? npcId = null;
            
            if (request.Params.TryGetValue("component_type", out var componentTypeObj))
            {
                componentType = componentTypeObj?.ToString();
            }
            
            if (request.Params.TryGetValue("npc_id", out var npcIdObj))
            {
                npcId = npcIdObj?.ToString();
            }

            GameObject? gameObject = null;
            
            // If npc_id is provided, get GameObject from NPC
            if (!string.IsNullOrEmpty(npcId))
            {
                gameObject = GetGameObjectFromNPCId(npcId);
                if (gameObject == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        "NPC not found or has no GameObject",
                        new { npc_id = npcId }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }
                objectName = gameObject.name; // Use GameObject name for consistency
            }
            else if (!string.IsNullOrEmpty(objectName))
            {
                gameObject = ReflectionHelper.FindGameObject(objectName);
                if (gameObject == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        "GameObject not found",
                        new { object_name = objectName }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }
            }
            else
            {
                var errorResponse = ProtocolHandler.CreateErrorResponse(
                    request.Id,
                    -32602, // Invalid params
                    "Either object_name or npc_id parameter is required"
                );
                _responseQueue.EnqueueResponse(errorResponse);
                return;
            }

            object? targetObject = gameObject;
            
            // If component_type is specified, get that component first
            if (!string.IsNullOrEmpty(componentType))
            {
                var component = FindComponentByTypeName(gameObject, componentType);
                if (component == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        "Component not found",
                        new { component_type = componentType, object_name = objectName }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }
                targetObject = component;
            }

            // Navigate the member path (supports nested access like "Dealer.Home.BuildingName")
            var pathParts = memberPath.Split('.');
            object? currentValue = targetObject;
            string currentPath = "";

            foreach (var part in pathParts)
            {
                if (currentValue == null)
                {
                    var errorResponse = ProtocolHandler.CreateErrorResponse(
                        request.Id,
                        -32000, // Game error
                        $"Null value encountered at path: {currentPath}",
                        new { member_path = memberPath, current_path = currentPath }
                    );
                    _responseQueue.EnqueueResponse(errorResponse);
                    return;
                }

                currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}.{part}";
                
                // Try property first, then field
                var property = currentValue.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (property != null && property.CanRead)
                {
                    try
                    {
                        currentValue = property.GetValue(currentValue);
                    }
                    catch (Exception ex)
                    {
                        var errorResponse = ProtocolHandler.CreateErrorResponse(
                            request.Id,
                            -32000, // Game error
                            $"Error reading property '{part}' at path '{currentPath}'",
                            new { member_path = memberPath, error = ex.Message }
                        );
                        _responseQueue.EnqueueResponse(errorResponse);
                        return;
                    }
                }
                else
                {
                    var field = currentValue.GetType().GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (field != null)
                    {
                        try
                        {
                            currentValue = field.GetValue(currentValue);
                        }
                        catch (Exception ex)
                        {
                            var errorResponse = ProtocolHandler.CreateErrorResponse(
                                request.Id,
                                -32000, // Game error
                                $"Error reading field '{part}' at path '{currentPath}'",
                                new { member_path = memberPath, error = ex.Message }
                            );
                            _responseQueue.EnqueueResponse(errorResponse);
                            return;
                        }
                    }
                    else
                    {
                        var errorResponse = ProtocolHandler.CreateErrorResponse(
                            request.Id,
                            -32000, // Game error
                            $"Member '{part}' not found at path '{currentPath}'",
                            new { member_path = memberPath, current_path = currentPath }
                        );
                        _responseQueue.EnqueueResponse(errorResponse);
                        return;
                    }
                }
            }

            // Format the final value
            var formattedValue = FormatValueDeep(currentValue, 2);

            var result = new Dictionary<string, object>
            {
                ["member_path"] = memberPath,
                ["value"] = formattedValue,
                ["value_type"] = currentValue?.GetType().FullName ?? "null",
                ["object_name"] = objectName,
                ["component_type"] = componentType ?? "GameObject"
            };

            var response = ProtocolHandler.CreateSuccessResponse(request.Id, result);
            _responseQueue.EnqueueResponse(response);
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Error in HandleGetMemberValue: {ex.Message}");
            var errorResponse = ProtocolHandler.CreateErrorResponse(
                request.Id,
                -32000, // Game error
                "Failed to get member value",
                new { details = ex.Message, stack_trace = ex.StackTrace }
            );
            _responseQueue.EnqueueResponse(errorResponse);
        }
    }

    /// <summary>
    /// Finds a component by type name (supports partial matching) on a GameObject.
    /// Uses multiple strategies: direct type resolution, GetComponent<T>, and reflection-based search.
    /// </summary>
    private Component? FindComponentByTypeName(GameObject gameObject, string typeName)
    {
        if (gameObject == null || string.IsNullOrEmpty(typeName))
            return null;

        // Strategy 1: Try to resolve the type and use GetComponent<T>
        try
        {
            Type? resolvedType = ResolveComponentType(typeName);
            if (resolvedType != null && typeof(Component).IsAssignableFrom(resolvedType))
            {
                // Use reflection to call GetComponent<T> with the resolved type
                try
                {
                    var getComponentMethod = typeof(GameObject).GetMethods()
                        .FirstOrDefault(m => m.Name == "GetComponent" && 
                                            m.IsGenericMethod && 
                                            m.GetParameters().Length == 0);
                    
                    if (getComponentMethod != null)
                    {
                        var genericMethod = getComponentMethod.MakeGenericMethod(resolvedType);
                        var component = genericMethod.Invoke(gameObject, null) as Component;
                        if (component != null)
                            return component;
                    }
                }
                catch
                {
                    // GetComponent<T> failed, try alternative approach
                }
            }
        }
        catch
        {
            // Type resolution failed, continue to next strategy
        }

        // Strategy 2: Search all components by name matching (most reliable)
        Component? bestMatch = null;
        int bestMatchScore = 0;

        try
        {
            var allComponents = gameObject.GetComponents<Component>();
            foreach (var component in allComponents)
            {
                if (component == null) continue;

                var componentType = component.GetType();
                var fullTypeName = componentType.FullName ?? "";
                var shortTypeName = componentType.Name;
                var namespaceName = componentType.Namespace ?? "";

                int matchScore = 0;

                // Exact full name match (highest priority - return immediately)
                if (string.Equals(fullTypeName, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return component;
                }

                // Exact short name match (high priority)
                if (string.Equals(shortTypeName, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    matchScore = 100;
                }
                // Full name ends with typeName (e.g., "ScheduleOne.NPCs.NPC" matches "NPC")
                else if (fullTypeName.EndsWith("." + typeName, StringComparison.OrdinalIgnoreCase))
                {
                    matchScore = 90;
                }
                // Full name contains (medium priority)
                else if (fullTypeName.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    matchScore = 50;
                }
                // Short name contains (lower priority)
                else if (shortTypeName.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    matchScore = 25;
                }
                // Namespace contains (lowest priority)
                else if (namespaceName.Contains(typeName, StringComparison.OrdinalIgnoreCase))
                {
                    matchScore = 10;
                }

                // Keep track of best match
                if (matchScore > bestMatchScore)
                {
                    bestMatch = component;
                    bestMatchScore = matchScore;
                }
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"Error searching components on {gameObject.name}: {ex.Message}");
        }

        return bestMatch;
    }

    /// <summary>
    /// Gets a GameObject from an NPC ID by looking up the NPC and accessing its gameObject property.
    /// </summary>
    private GameObject? GetGameObjectFromNPCId(string npcId)
    {
        try
        {
            object? foundNPC = null;
            
            // Look up NPC in registry
            if (S1NPCManager.NPCRegistry != null)
            {
                foreach (var npc in S1NPCManager.NPCRegistry)
                {
                    if (npc == null) continue;
                    
                    // Try to get NPC ID
                    string currentNpcId = "";
                    try
                    {
                        var idProperty = npc.GetType().GetProperty("ID") ?? 
                                       npc.GetType().GetProperty("Id") ?? 
                                       npc.GetType().GetProperty("NPCID");
                        if (idProperty != null)
                        {
                            currentNpcId = idProperty.GetValue(npc)?.ToString() ?? "";
                        }
                    }
                    catch { }
                    
                    if (currentNpcId.Equals(npcId, StringComparison.OrdinalIgnoreCase))
                    {
                        foundNPC = npc;
                        break;
                    }
                }
            }

            if (foundNPC == null)
                return null;

            // Get GameObject from NPC
            var gameObjectProperty = foundNPC.GetType().GetProperty("gameObject");
            if (gameObjectProperty != null)
            {
                return gameObjectProperty.GetValue(foundNPC) as GameObject;
            }
        }
        catch (Exception ex)
        {
            ModLogger.Debug($"Error getting GameObject from NPC ID {npcId}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Resolves a component type by name, searching through all loaded assemblies.
    /// </summary>
    private Type? ResolveComponentType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Search through all loaded assemblies
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                // Try exact full name match first
                var type = assembly.GetType(typeName, false, true);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return type;

                // Try with case-insensitive search
                type = assembly.GetType(typeName, false, false);
                if (type != null && typeof(Component).IsAssignableFrom(type))
                    return type;

                // Search all types in assembly for partial match
                try
                {
                    var types = assembly.GetTypes();
                    foreach (var t in types)
                    {
                        if (!typeof(Component).IsAssignableFrom(t))
                            continue;

                        // Check if name matches
                        if (string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase) ||
                            (t.FullName != null && t.FullName.Contains(typeName, StringComparison.OrdinalIgnoreCase)))
                        {
                            return t;
                        }
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Some types might not be loadable, continue searching
                }
            }
            catch
            {
                // Assembly might not be accessible, continue
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a component by type name in the entire scene.
    /// </summary>
    private Component? FindComponentByTypeNameInScene(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        // Search all GameObjects in the scene
        var allObjects = ReflectionHelper.FindAllGameObjects();
        foreach (var gameObject in allObjects)
        {
            var component = FindComponentByTypeName(gameObject, typeName);
            if (component != null)
                return component;
        }

        return null;
    }

    /// <summary>
    /// Enhanced component inspection with deep reflection support.
    /// </summary>
    private Dictionary<string, object> InspectComponentDeep(Component component, int maxDepth = 3)
    {
        var result = new Dictionary<string, object>
        {
            ["type"] = component.GetType().Name,
            ["full_type"] = component.GetType().FullName ?? "Unknown"
        };

        // Get all properties (including non-public)
        var properties = new Dictionary<string, object>();
        try
        {
            var props = component.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var prop in props)
            {
                try
                {
                    if (IsIntPtrType(prop.PropertyType))
                        continue;

                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        var value = prop.GetValue(component);
                        var formattedValue = FormatValueDeep(value, maxDepth - 1);
                        if (formattedValue != null)
                        {
                            properties[prop.Name] = formattedValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    properties[prop.Name] = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["readable"] = false
                    };
                }
            }
        }
        catch (Exception ex)
        {
            properties["_error"] = ex.Message;
        }
        result["properties"] = properties;

        // Get all fields (including non-public)
        var fields = new Dictionary<string, object>();
        try
        {
            var fieldInfos = component.GetType().GetFields(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            
            foreach (var field in fieldInfos)
            {
                try
                {
                    if (IsIntPtrType(field.FieldType))
                        continue;

                    var value = ReflectionHelper.GetFieldValue(component, field.Name);
                    var formattedValue = FormatValueDeep(value, maxDepth - 1);
                    if (formattedValue != null)
                    {
                        fields[field.Name] = formattedValue;
                    }
                }
                catch (Exception ex)
                {
                    fields[field.Name] = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["readable"] = false
                    };
                }
            }
        }
        catch (Exception ex)
        {
            fields["_error"] = ex.Message;
        }
        result["fields"] = fields;

        return result;
    }

    /// <summary>
    /// Enhanced value formatting that handles arrays, lists, dictionaries, and nested objects.
    /// </summary>
    private object? FormatValueDeep(object? value, int remainingDepth)
    {
        if (value == null)
            return "null";

        if (remainingDepth <= 0)
            return "[max depth reached]";

        var type = value.GetType();

        // Skip IntPtr values
        if (IsIntPtrType(type))
            return null;

        // Primitives and strings
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return value;

        // Unity Objects
        if (value is UnityEngine.Object unityObj)
            return $"{unityObj.GetType().Name}:{unityObj.name}";

        // Arrays
        if (type.IsArray)
        {
            var array = value as Array;
            if (array != null)
            {
                var items = new List<object?>();
                for (int i = 0; i < Math.Min(array.Length, 50); i++) // Limit to 50 items
                {
                    items.Add(FormatValueDeep(array.GetValue(i), remainingDepth - 1));
                }
                return new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["element_type"] = type.GetElementType()?.Name ?? "unknown",
                    ["length"] = array.Length,
                    ["items"] = items,
                    ["truncated"] = array.Length > 50
                };
            }
        }

        // Lists (IList)
        if (value is System.Collections.IList list)
        {
            var items = new List<object?>();
            for (int i = 0; i < Math.Min(list.Count, 50); i++) // Limit to 50 items
            {
                items.Add(FormatValueDeep(list[i], remainingDepth - 1));
            }
            return new Dictionary<string, object>
            {
                ["type"] = "list",
                ["count"] = list.Count,
                ["items"] = items,
                ["truncated"] = list.Count > 50
            };
        }

        // Dictionaries (IDictionary)
        if (value is System.Collections.IDictionary dict)
        {
            var entries = new Dictionary<string, object?>();
            int count = 0;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (count >= 50) break; // Limit to 50 entries
                var keyStr = entry.Key?.ToString() ?? "null";
                entries[keyStr] = FormatValueDeep(entry.Value, remainingDepth - 1);
                count++;
            }
            return new Dictionary<string, object>
            {
                ["type"] = "dictionary",
                ["count"] = dict.Count,
                ["entries"] = entries,
                ["truncated"] = dict.Count > 50
            };
        }

        // Enums
        if (type.IsEnum)
            return value.ToString();

        // Complex objects - try to extract key properties/fields
        try
        {
            var objData = new Dictionary<string, object>
            {
                ["type"] = type.Name,
                ["full_type"] = type.FullName ?? "Unknown"
            };

            // Get a few key properties
            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var keyProps = new Dictionary<string, object?>();
            int propCount = 0;
            foreach (var prop in props)
            {
                if (propCount >= 10) break; // Limit to 10 properties
                if (prop.CanRead && prop.GetIndexParameters().Length == 0 && !IsIntPtrType(prop.PropertyType))
                {
                    try
                    {
                        var propValue = prop.GetValue(value);
                        keyProps[prop.Name] = FormatValueDeep(propValue, remainingDepth - 1);
                        propCount++;
                    }
                    catch { }
                }
            }
            if (keyProps.Count > 0)
                objData["properties"] = keyProps;

            // Get a few key fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var keyFields = new Dictionary<string, object?>();
            int fieldCount = 0;
            foreach (var field in fields)
            {
                if (fieldCount >= 10) break; // Limit to 10 fields
                if (!IsIntPtrType(field.FieldType))
                {
                    try
                    {
                        var fieldValue = field.GetValue(value);
                        keyFields[field.Name] = FormatValueDeep(fieldValue, remainingDepth - 1);
                        fieldCount++;
                    }
                    catch { }
                }
            }
            if (keyFields.Count > 0)
                objData["fields"] = keyFields;

            return objData;
        }
        catch
        {
            // Fallback to ToString()
            try
            {
                return value.ToString() ?? "null";
            }
            catch
            {
                return "[unable to format]";
            }
        }
    }
}

