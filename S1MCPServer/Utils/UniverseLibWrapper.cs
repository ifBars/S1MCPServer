using System;
using System.Reflection;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace S1MCPServer.Utils;

/// <summary>
/// Wrapper for UniverseLib utilities that uses reflection to access UniverseLib when available.
/// Provides graceful fallback when UniverseLib is not present.
/// </summary>
internal static class UniverseLibWrapper
{
    private static bool? _isAvailable;
    private static Type? _unityUtilityType;
    private static Type? _reflectionUtilityType;

    /// <summary>
    /// Checks if UniverseLib is available in the current runtime.
    /// </summary>
    public static bool IsAvailable
    {
        get
        {
            if (_isAvailable.HasValue)
                return _isAvailable.Value;

            try
            {
                // Try to find UniverseLib assembly
                var assembly = Assembly.Load("UniverseLib");
                if (assembly != null)
                {
                    _unityUtilityType = assembly.GetType("UniverseLib.Utility.UnityUtility");
                    _reflectionUtilityType = assembly.GetType("UniverseLib.Utility.ReflectionUtility");
                    _isAvailable = _unityUtilityType != null || _reflectionUtilityType != null;
                }
                else
                {
                    _isAvailable = false;
                }
            }
            catch
            {
                _isAvailable = false;
            }

            return _isAvailable.Value;
        }
    }

    /// <summary>
    /// Finds a GameObject by name using UniverseLib if available.
    /// </summary>
    public static GameObject? FindGameObjectByName(string name)
    {
        if (!IsAvailable || _unityUtilityType == null)
            return null;

        try
        {
            var method = _unityUtilityType.GetMethod("FindGameObjectByName", 
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);

            if (method != null)
            {
                var result = method.Invoke(null, new object[] { name });
                return result as GameObject;
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Gets a component using UniverseLib if available.
    /// </summary>
    public static T? GetComponent<T>(GameObject obj) where T : Component
    {
        if (!IsAvailable || _unityUtilityType == null || obj == null)
            return null;

        try
        {
            var method = _unityUtilityType.GetMethod("GetComponent",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(GameObject) },
                null);

            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(typeof(T));
                var result = genericMethod.Invoke(null, new object[] { obj });
                return result as T;
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Gets all GameObjects using UniverseLib if available.
    /// </summary>
    public static GameObject[]? GetAllGameObjects()
    {
        if (!IsAvailable || _unityUtilityType == null)
            return null;

        try
        {
            var method = _unityUtilityType.GetMethod("GetAllGameObjects",
                BindingFlags.Public | BindingFlags.Static);

            if (method != null)
            {
                var result = method.Invoke(null, null);
                return result as GameObject[];
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Finds all objects of type T using UniverseLib if available.
    /// </summary>
    public static T[]? FindObjectsOfTypeAll<T>() where T : UnityEngine.Object
    {
        if (!IsAvailable || _unityUtilityType == null)
            return null;

        try
        {
            var method = _unityUtilityType.GetMethod("FindObjectsOfTypeAll",
                BindingFlags.Public | BindingFlags.Static);

            if (method != null)
            {
                var genericMethod = method.MakeGenericMethod(typeof(T));
                var result = genericMethod.Invoke(null, null);
                return result as T[];
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Gets a field value using UniverseLib reflection if available.
    /// </summary>
    public static object? GetFieldValue(object obj, Type type, string fieldName, bool includeNonPublic)
    {
        if (!IsAvailable || _reflectionUtilityType == null || obj == null)
            return null;

        try
        {
            var getFieldMethod = _reflectionUtilityType.GetMethod("GetField",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type), typeof(string), typeof(bool) },
                null);

            var getValueMethod = _reflectionUtilityType.GetMethod("GetValue",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(FieldInfo), typeof(object) },
                null);

            if (getFieldMethod != null && getValueMethod != null)
            {
                var field = getFieldMethod.Invoke(null, new object[] { type, fieldName, includeNonPublic }) as FieldInfo;
                if (field != null)
                {
                    return getValueMethod.Invoke(null, new object[] { field, obj });
                }
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Gets a property value using UniverseLib reflection if available.
    /// </summary>
    public static object? GetPropertyValue(object obj, Type type, string propertyName, bool includeNonPublic)
    {
        if (!IsAvailable || _reflectionUtilityType == null || obj == null)
            return null;

        try
        {
            var getPropertyMethod = _reflectionUtilityType.GetMethod("GetProperty",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type), typeof(string), typeof(bool) },
                null);

            var getValueMethod = _reflectionUtilityType.GetMethod("GetValue",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(PropertyInfo), typeof(object) },
                null);

            if (getPropertyMethod != null && getValueMethod != null)
            {
                var property = getPropertyMethod.Invoke(null, new object[] { type, propertyName, includeNonPublic }) as PropertyInfo;
                if (property != null)
                {
                    return getValueMethod.Invoke(null, new object[] { property, obj });
                }
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return null;
    }

    /// <summary>
    /// Sets a field value using UniverseLib reflection if available.
    /// </summary>
    public static bool SetFieldValue(object obj, Type type, string fieldName, object? value, bool includeNonPublic)
    {
        if (!IsAvailable || _reflectionUtilityType == null || obj == null)
            return false;

        try
        {
            var getFieldMethod = _reflectionUtilityType.GetMethod("GetField",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type), typeof(string), typeof(bool) },
                null);

            var setValueMethod = _reflectionUtilityType.GetMethod("SetValue",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(FieldInfo), typeof(object), typeof(object) },
                null);

            if (getFieldMethod != null && setValueMethod != null)
            {
                var field = getFieldMethod.Invoke(null, new object[] { type, fieldName, includeNonPublic }) as FieldInfo;
                if (field != null)
                {
                    setValueMethod.Invoke(null, new object[] { field, obj, value });
                    return true;
                }
            }
        }
        catch
        {
            // UniverseLib method not available or failed
        }

        return false;
    }

    /// <summary>
    /// Attempts to cast an object to type T using UniverseLib if available.
    /// Provides enhanced cross-runtime casting support.
    /// </summary>
    public static T? TryCast<T>(object? obj)
#if !MONO
        where T : Il2CppSystem.Object
#else
        where T : class
#endif
    {
        if (obj == null)
            return null;

        try
        {
            // Try direct cast first (works for Mono and some IL2CPP cases)
            if (obj is T directCast)
                return directCast;

#if !MONO
            // For IL2CPP, try using Il2CppInterop if available
            if (obj is Il2CppSystem.Object il2CppObj)
            {
                try
                {
                    return il2CppObj.TryCast<T>();
                }
                catch
                {
                    // TryCast failed, continue to fallback
                }
            }
#endif

            // Try UniverseLib's casting utilities if available
            // UniverseLib may have additional casting methods
            // For now, fall back to standard casting
        }
        catch
        {
            // Casting failed
        }

        return null;
    }

    /// <summary>
    /// Checks if an object can be cast to type T using UniverseLib if available.
    /// </summary>
    public static bool CanCast<T>(object? obj)
#if !MONO
        where T : Il2CppSystem.Object
#else
        where T : class
#endif
    {
        if (obj == null)
            return false;

        // Try the cast to see if it works
        return TryCast<T>(obj) != null;
    }

    /// <summary>
    /// Gets the Il2CppType (as a Type object) for a given Type using Il2CppInterop.
    /// This is a helper method that avoids type inference issues.
    /// Note: Il2CppType.Of() returns a Type object, not an Il2CppType instance.
    /// </summary>
    public static Type? GetIl2CppType(Type type)
    {
        if (type == null)
            return null;

#if !MONO
        try
        {
            // Il2CppType.Of<T>() is a generic method, so we need to use reflection
            // to call it with the type parameter from the Type object
            var ofMethod = typeof(Il2CppType).GetMethod("Of", BindingFlags.Public | BindingFlags.Static);
            if (ofMethod == null)
                return null;

            // Make the generic method with the specific type
            var genericMethod = ofMethod.MakeGenericMethod(type);
            
            // Invoke with default parameter (bool throwOnFailure = false)
            var result = genericMethod.Invoke(null, new object[] { false });
            return result as Type;
        }
        catch
        {
            // Type is not an IL2CPP type or conversion failed
            return null;
        }
#else
        return null;
#endif
    }
}

