using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using S1MCPServer.Utils;
using UnityEngine;

namespace S1MCPServer.Integrations;

/// <summary>
/// Optional integration with UnityExplorer's InspectorManager API.
/// Uses reflection to access UnityExplorer when available, with graceful fallback.
/// </summary>
public static class UnityExplorerIntegration
{
    private static bool? _isAvailable;
    private static Type? _inspectorManagerType;
    private static MethodInfo? _inspectObjectMethod;
    private static MethodInfo? _inspectTypeMethod;

    /// <summary>
    /// Checks if UnityExplorer is available in the current runtime.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            if (_isAvailable.HasValue)
                return _isAvailable.Value;

            try
            {
                // Check MelonLoader mod list first
                var unityExplorerMod = MelonMod.RegisteredMelons
                    .FirstOrDefault(m => m.Info?.Name?.Contains("UnityExplorer", StringComparison.OrdinalIgnoreCase) == true);

                if (unityExplorerMod != null)
                {
                    ModLogger.Debug("UnityExplorer detected via MelonLoader mod list");
                }

                // Try to find UnityExplorer assembly
                Assembly? unityExplorerAssembly = null;
                try
                {
                    unityExplorerAssembly = Assembly.Load("UnityExplorer");
                }
                catch
                {
                    // Try alternative assembly names
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name?.Contains("UnityExplorer", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            unityExplorerAssembly = assembly;
                            break;
                        }
                    }
                }

                if (unityExplorerAssembly != null)
                {
                    _inspectorManagerType = unityExplorerAssembly.GetType("UnityExplorer.InspectorManager");
                    if (_inspectorManagerType != null)
                    {
                        _inspectObjectMethod = _inspectorManagerType.GetMethod("Inspect",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(object) },
                            null);

                        _inspectTypeMethod = _inspectorManagerType.GetMethod("Inspect",
                            BindingFlags.Public | BindingFlags.Static,
                            null,
                            new[] { typeof(Type) },
                            null);

                        _isAvailable = _inspectObjectMethod != null || _inspectTypeMethod != null;
                        
                        if (_isAvailable.Value)
                        {
                            ModLogger.Info("UnityExplorer InspectorManager API available");
                        }
                    }
                }

                if (!_isAvailable.HasValue)
                {
                    _isAvailable = false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"Error checking UnityExplorer availability: {ex.Message}");
                _isAvailable = false;
            }

            return _isAvailable.Value;
        }
    }

    /// <summary>
    /// Inspects an object using UnityExplorer's InspectorManager if available.
    /// Falls back to ReflectionHelper if UnityExplorer is not loaded.
    /// </summary>
    /// <param name="obj">The object to inspect.</param>
    /// <returns>Inspection data dictionary, or null if inspection failed.</returns>
    public static Dictionary<string, object>? InspectObject(object? obj)
    {
        if (obj == null)
            return null;

        // Try UnityExplorer InspectorManager first if available
        if (IsAvailable && _inspectObjectMethod != null)
        {
            try
            {
                _inspectObjectMethod.Invoke(null, new object[] { obj });
                
                // UnityExplorer's Inspect method opens a UI window, so we return basic info
                // and use ReflectionHelper for detailed data
                return new Dictionary<string, object>
                {
                    ["inspected_via"] = "UnityExplorer",
                    ["object_type"] = obj.GetType().FullName ?? "Unknown",
                    ["object_to_string"] = obj.ToString() ?? "null"
                };
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"UnityExplorer Inspect failed: {ex.Message}, falling back to ReflectionHelper");
            }
        }

        // Fallback to ReflectionHelper-based inspection
        return InspectObjectWithReflection(obj);
    }

    /// <summary>
    /// Inspects a Type using UnityExplorer's InspectorManager if available.
    /// </summary>
    /// <param name="type">The type to inspect.</param>
    /// <returns>True if inspection was successful, false otherwise.</returns>
    public static bool InspectType(Type? type)
    {
        if (type == null)
            return false;

        if (IsAvailable && _inspectTypeMethod != null)
        {
            try
            {
                _inspectTypeMethod.Invoke(null, new object[] { type });
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.Debug($"UnityExplorer InspectType failed: {ex.Message}");
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Inspects an object using ReflectionHelper (fallback when UnityExplorer not available).
    /// </summary>
    private static Dictionary<string, object>? InspectObjectWithReflection(object obj)
    {
        try
        {
            var result = new Dictionary<string, object>
            {
                ["inspected_via"] = "ReflectionHelper",
                ["object_type"] = obj.GetType().FullName ?? "Unknown",
                ["object_to_string"] = obj.ToString() ?? "null"
            };

            // If it's a GameObject, get component information
            if (obj is GameObject gameObject)
            {
                var components = new List<Dictionary<string, object>>();
                var allComponents = gameObject.GetComponents<Component>();
                
                foreach (var component in allComponents)
                {
                    if (component == null) continue;

                    var componentData = new Dictionary<string, object>
                    {
                        ["type"] = component.GetType().Name,
                        ["full_type"] = component.GetType().FullName ?? "Unknown"
                    };

                    // Try to get some basic properties
                    var properties = new Dictionary<string, object>();
                    try
                    {
                        var props = component.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                        foreach (var prop in props.Take(10)) // Limit to first 10 properties
                        {
                            try
                            {
                                if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                                {
                                    var value = prop.GetValue(component);
                                    properties[prop.Name] = value?.ToString() ?? "null";
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
                    components.Add(componentData);
                }

                result["components"] = components;
                result["children"] = ReflectionHelper.GetObjectHierarchy(gameObject);
            }

            return result;
        }
        catch (Exception ex)
        {
            ModLogger.Error($"ReflectionHelper inspection failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets detailed inspection data for an object, preferring UnityExplorer if available.
    /// </summary>
    /// <param name="obj">The object to inspect.</param>
    /// <returns>Detailed inspection data dictionary.</returns>
    public static Dictionary<string, object> GetDetailedInspection(object? obj)
    {
        if (obj == null)
        {
            return new Dictionary<string, object> { ["error"] = "Object is null" };
        }

        // Always use ReflectionHelper for detailed data
        // UnityExplorer's Inspect opens a UI, so we use ReflectionHelper for programmatic access
        var result = InspectObjectWithReflection(obj);
        if (result != null)
        {
            result["unityexplorer_available"] = IsAvailable;
            return result;
        }

        return new Dictionary<string, object>
        {
            ["error"] = "Failed to inspect object",
            ["object_type"] = obj.GetType().FullName ?? "Unknown"
        };
    }
}

