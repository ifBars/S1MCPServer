using System.Reflection;
using UnityEngine;
#if !MONO
using Il2CppInterop.Runtime;
using Object = Il2CppSystem.Object;
#endif

namespace S1MCPServer.Utils;

/// <summary>
/// Provides reflection utilities for accessing Unity GameObjects and components.
/// Handles both Mono and IL2CPP backends using conditional compilation.
/// Enhanced with UniverseLib for improved cross-runtime compatibility.
/// </summary>
public static class ReflectionHelper
{
    /// <summary>
    /// Finds a GameObject by name (works in both Mono and IL2CPP).
    /// Uses UniverseLib for enhanced cross-runtime support when available.
    /// </summary>
    /// <param name="name">The name of the GameObject to find.</param>
    /// <returns>The GameObject if found, null otherwise.</returns>
    public static GameObject? FindGameObject(string name)
    {
        // Try UniverseLib first if available
        var universeLibResult = UniverseLibWrapper.FindGameObjectByName(name);
        if (universeLibResult != null)
            return universeLibResult;

#if MONO
        return GameObject.Find(name);
#else
        // IL2CPP: Use Resources.FindObjectsOfTypeAll to find all GameObjects
        // Then filter by name
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var obj in allObjects)
        {
            if (obj.name == name)
            {
                return obj;
            }
        }
        return null;
#endif
    }

    /// <summary>
    /// Gets a component of type T from a GameObject (handles both Mono and IL2CPP).
    /// Uses UniverseLib for enhanced cross-runtime type handling when available.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="obj">The GameObject to get the component from.</param>
    /// <returns>The component if found, null otherwise.</returns>
    public static T? GetComponent<T>(GameObject obj) where T : Component
    {
        if (obj == null)
        {
            return null;
        }

        // Try UniverseLib first if available
        var universeLibResult = UniverseLibWrapper.GetComponent<T>(obj);
        if (universeLibResult != null)
            return universeLibResult;

#if MONO
        return obj.GetComponent<T>();
#else
        // IL2CPP: Il2CppInterop should handle GetComponent<T>() directly
        // If that doesn't work, fall back to reflection-based approach
        try
        {
            return obj.GetComponent<T>();
        }
        catch
        {
            // Fallback: Use reflection to call GetComponent
            try
            {
                var method = typeof(GameObject).GetMethod("GetComponent", new[] { typeof(Type) });
                if (method != null)
                {
                    var component = method.Invoke(obj, new object[] { typeof(T) });
                    if (component != null)
                    {
                        // In IL2CPP, component should be an Il2CppSystem.Object, use TryCast
                        if (component is Object il2CppComponent)
                        {
                            return il2CppComponent.TryCast<T>();
                        }
                        // Fallback: try direct cast
                        if (component is T directCast)
                        {
                            return directCast;
                        }
                    }
                }
            }
            catch
            {
                // Component not found or method not available
            }
            return null;
        }
#endif
    }

    /// <summary>
    /// Gets all components of type T from a GameObject and its children.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="obj">The GameObject to search.</param>
    /// <returns>List of components found.</returns>
    public static List<T> GetAllComponents<T>(GameObject obj) where T : Component
    {
        var results = new List<T>();
        if (obj == null)
        {
            return results;
        }

        // Get component from this object
        var component = GetComponent<T>(obj);
        if (component != null)
        {
            results.Add(component);
        }

        // Get components from children
        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            results.AddRange(GetAllComponents<T>(child.gameObject));
        }

        return results;
    }

    /// <summary>
    /// Gets the value of a private field from an object using reflection.
    /// Uses UniverseLib for enhanced cross-runtime field access when available.
    /// </summary>
    /// <param name="obj">The object to get the field from.</param>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The field value, or null if not found.</returns>
    public static object? GetPrivateField(object obj, string fieldName)
    {
        if (obj == null)
        {
            return null;
        }

        // Try UniverseLib first if available
        var universeLibResult = UniverseLibWrapper.GetFieldValue(obj, obj.GetType(), fieldName, true);
        if (universeLibResult != null)
            return universeLibResult;

        var field = obj.GetType().GetField(
            fieldName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        );

        return field?.GetValue(obj);
    }

    /// <summary>
    /// Gets the value of a private property from an object using reflection.
    /// Uses UniverseLib for enhanced cross-runtime property access when available.
    /// </summary>
    /// <param name="obj">The object to get the property from.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>The property value, or null if not found.</returns>
    public static object? GetPrivateProperty(object obj, string propertyName)
    {
        if (obj == null)
        {
            return null;
        }

        // Try UniverseLib first if available
        var universeLibResult = UniverseLibWrapper.GetPropertyValue(obj, obj.GetType(), propertyName, true);
        if (universeLibResult != null)
            return universeLibResult;

        var property = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        );

        return property?.GetValue(obj);
    }

    /// <summary>
    /// Gets the object hierarchy (children) of a GameObject.
    /// </summary>
    /// <param name="obj">The GameObject to get the hierarchy from.</param>
    /// <returns>List of child GameObject names.</returns>
    public static List<string> GetObjectHierarchy(GameObject obj)
    {
        var hierarchy = new List<string>();
        if (obj == null)
        {
            return hierarchy;
        }

        for (int i = 0; i < obj.transform.childCount; i++)
        {
            var child = obj.transform.GetChild(i);
            hierarchy.Add(child.name);
        }

        return hierarchy;
    }

    /// <summary>
    /// Safely gets a field value, handling both Mono and IL2CPP types.
    /// </summary>
    /// <param name="obj">The object to get the field from.</param>
    /// <param name="fieldName">The name of the field.</param>
    /// <returns>The field value, or null if not found.</returns>
    public static object? GetFieldValue(object obj, string fieldName)
    {
        return GetPrivateField(obj, fieldName);
    }

    /// <summary>
    /// Safely sets a field value, handling both Mono and IL2CPP types.
    /// Uses UniverseLib for enhanced cross-runtime field setting when available.
    /// </summary>
    /// <param name="obj">The object to set the field on.</param>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="value">The value to set.</param>
    /// <returns>True if the field was set successfully, false otherwise.</returns>
    public static bool SetFieldValue(object obj, string fieldName, object? value)
    {
        if (obj == null)
        {
            return false;
        }

        // Try UniverseLib first if available
        if (UniverseLibWrapper.SetFieldValue(obj, obj.GetType(), fieldName, value, true))
            return true;

        try
        {
            var field = obj.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
            );

            if (field != null)
            {
#if MONO
                if (field.CanWrite)
                {
                    field.SetValue(obj, value);
                    return true;
                }
#else
                // IL2CPP: Try to set value directly (CanWrite might not be available)
                try
                {
                    field.SetValue(obj, value);
                    return true;
                }
                catch
                {
                    // Field cannot be written
                }
#endif
            }
        }
        catch
        {
            // Field not found or cannot be written
        }

        return false;
    }

    /// <summary>
    /// Finds all GameObjects in the scene (enhanced with UniverseLib when available).
    /// </summary>
    /// <returns>List of all GameObjects in the scene.</returns>
    public static List<GameObject> FindAllGameObjects()
    {
        var results = new List<GameObject>();

        // Try UniverseLib first if available
        var universeLibObjects = UniverseLibWrapper.GetAllGameObjects();
        if (universeLibObjects != null)
        {
            results.AddRange(universeLibObjects);
            return results;
        }

        // Fallback: Use Resources.FindObjectsOfTypeAll
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        results.AddRange(objects);
        return results;
    }

    /// <summary>
    /// Finds GameObjects by type (enhanced with UniverseLib when available).
    /// </summary>
    /// <typeparam name="T">The component type to search for.</typeparam>
    /// <returns>List of GameObjects with the specified component type.</returns>
    public static List<GameObject> FindGameObjectsByType<T>() where T : Component
    {
        var results = new List<GameObject>();

        // Try UniverseLib first if available
        var universeLibComponents = UniverseLibWrapper.FindObjectsOfTypeAll<T>();
        if (universeLibComponents != null)
        {
            foreach (var component in universeLibComponents)
            {
                if (component != null && component is Component comp && comp.gameObject != null)
                {
                    results.Add(comp.gameObject);
                }
            }
            return results;
        }

        // Fallback: Use standard Unity methods
#if MONO
        var components = UnityEngine.Object.FindObjectsOfType<T>();
#else
        // IL2CPP: Use UnityEngine.Object explicitly (not Il2CppSystem.Object)
        var components = UnityEngine.Object.FindObjectsOfType<T>();
#endif
        foreach (var component in components)
        {
            if (component != null && component.gameObject != null)
            {
                results.Add(component.gameObject);
            }
        }
        return results;
    }
}


