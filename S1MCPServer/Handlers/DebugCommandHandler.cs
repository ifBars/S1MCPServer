using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using S1MCPServer.Core;
using S1MCPServer.Integrations;
using S1MCPServer.Models;
using S1MCPServer.Utils;
using UnityEngine;

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
        var components = new List<Dictionary<string, object>>();
        var allComponents = gameObject.GetComponents<Component>();
        
        foreach (var component in allComponents)
        {
            if (component == null) continue;

            var componentData = InspectComponent(component);
            components.Add(componentData);
        }

        result["components"] = components;
        result["component_count"] = components.Count;

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
                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        var value = prop.GetValue(component);
                        properties[prop.Name] = FormatValue(value);
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
                    var value = ReflectionHelper.GetFieldValue(component, field.Name);
                    fields[field.Name] = FormatValue(value);
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

    private object FormatValue(object? value)
    {
        if (value == null)
            return "null";

        var type = value.GetType();
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
            return value;

        if (value is UnityEngine.Object unityObj)
            return $"{unityObj.GetType().Name}:{unityObj.name}";

        return value.ToString() ?? "null";
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
}

