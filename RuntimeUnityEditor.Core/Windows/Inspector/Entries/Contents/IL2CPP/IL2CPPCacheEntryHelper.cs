#if IL2CPP
using Il2CppInterop.Runtime.InteropTypes;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

/// <summary>
/// Utilities for making interacting with IL2CPP types easier and faster.
/// </summary>
public class IL2CPPCacheEntryHelper
{
    private static readonly Dictionary<Type, Dictionary<MemberInfo, FieldInfo>> PtrLookup = new();

    /// <summary>
    /// Creates a lookup table mapping method and field information to their corresponding pointers for a specified type.
    /// </summary>
    /// <param name="type">The type for which the pointer lookup table is created. If not a Il2CppObjectBase, empty collection is returned.</param>
    /// <returns>A dictionary containing the mapping of MemberInfo to FieldInfo pointers.</returns>
    public static Dictionary<MemberInfo, FieldInfo> GetPtrLookupTable(Type type)
    {
        // todo some way to clean up old entries?
        if (PtrLookup.TryGetValue(type, out var lookup))
            return lookup;

        lookup = new Dictionary<MemberInfo, FieldInfo>();
        PtrLookup[type] = lookup;

        if (!type.IsAssignableTo(typeof(Il2CppObjectBase)))
            return lookup;

        foreach (var methodInfo in type.GetAllMethods(Extensions.GetAllType.Both))
        {
            if (methodInfo.GetMethodBody() != null)
            {
                var ptr = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(methodInfo) ?? Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(methodInfo);
                if (ptr != null)
                    lookup[methodInfo] = ptr;
            }
        }

        return lookup;
    }

    /// <summary>
    /// Attempts to retrieve the IL2CPP cache entry for the specified event.
    /// </summary>
    /// <param name="instance">The instance of the object associated with the event.</param>
    /// <param name="type">The type of the event's declaring class.</param>
    /// <param name="p">The member information for which the cache entry is being retrieved.</param>
    /// <param name="lookup">A dictionary mapping member information to field information for lookup.</param>
    /// <param name="result">The resulting cache entry if found; otherwise, null.</param>
    /// <returns>True if the cache entry was successfully retrieved; otherwise, false.</returns>
    public static bool TryGetIl2CppCacheEntry(object instance, Type type, EventInfo p, Dictionary<MemberInfo, FieldInfo> lookup, out ICacheEntry result)
    {
        FieldInfo ptrAdd = null;
        FieldInfo ptrRaise = null;
        FieldInfo ptrRemove = null;
        var addMethod = p.GetAddMethod(true);
        if (addMethod != null) lookup.TryGetValue(addMethod, out ptrAdd);
        var raiseMethod = p.GetRaiseMethod(true);
        if (raiseMethod != null) lookup.TryGetValue(raiseMethod, out ptrRaise);
        var removeMethod = p.GetRemoveMethod(true);
        if (removeMethod != null) lookup.TryGetValue(removeMethod, out ptrRemove);
        if (ptrAdd != null || ptrRaise != null || ptrRemove != null)
        {
            result = new IL2CPPEventCacheEntry(instance, p, type, ptrAdd, ptrRaise, ptrRemove);
            return true;
        }

        result = null;
        return false;
    }

    /// <inheritdoc cref="TryGetIl2CppCacheEntry(object,System.Type,System.Reflection.EventInfo,System.Collections.Generic.Dictionary{System.Reflection.MemberInfo,System.Reflection.FieldInfo},out RuntimeUnityEditor.Core.Inspector.Entries.ICacheEntry)" />
    public static bool TryGetIl2CppCacheEntry(object instance, Type type, PropertyInfo p, Dictionary<MemberInfo, FieldInfo> lookup, out ICacheEntry result)
    {
        if (lookup.TryGetValue(p, out var ptr))
        {
            result = new IL2CPPFieldCacheEntry(instance, p, type, ptr);
            return true;
        }

        FieldInfo ptrGet = null;
        FieldInfo ptrSet = null;
        var getMethod = p.GetGetMethod(true);
        if (getMethod != null) lookup.TryGetValue(getMethod, out ptrGet);
        var setMethod = p.GetSetMethod(true);
        if (setMethod != null) lookup.TryGetValue(setMethod, out ptrSet);
        if (ptrGet != null || ptrSet != null)
        {
            result = new IL2CPPPropertyCacheEntry(instance, p, type, ptrGet, ptrSet);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Safely retrieve value of an interop pointer field from a specified type, handling potential exceptions and special cases.
    /// Either returns the pointer value or a string. For use in inspector.
    /// </summary>
    /// <param name="owner">The type that contains the static field.</param>
    /// <param name="ptrField">The ptr field to retrieve.</param>
    public static object SafeGetPtr(Type owner, FieldInfo ptrField)
    {
        if (ptrField == null) return "null";
        if (owner.ContainsGenericParameters)
            return "???";
        try
        {
            return ptrField.GetValue(null);
        }
        catch
        {
            return "error";
        }
    }

    internal static bool IsIl2CppCacheEntry(ICacheEntry entry)
    {
        return entry is IL2CPPFieldCacheEntry || entry is IL2CPPPropertyCacheEntry || entry is IL2CPPMethodCacheEntry || entry is IL2CPPEventCacheEntry;
    }
}

#endif
