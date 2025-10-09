#if IL2CPP
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

public class IL2CPPCacheEntryHelper
{
    private static readonly Dictionary<Type, Dictionary<MemberInfo, FieldInfo>> _ptrLookup = new();

    public static Dictionary<MemberInfo, FieldInfo> GetPtrLookupTable(Type type)
    {
        // todo some way to clean up old entries?
        if (_ptrLookup.TryGetValue(type, out var lookup))
            return lookup;
        
        lookup = new Dictionary<MemberInfo, FieldInfo>();
        _ptrLookup[type] = lookup;

        var staticFields = new List<FieldInfo>();
        var isIl2Cpp = false;
        var currentType = type;
        do
        {
            if (currentType == typeof(Il2CppObjectBase))
            {
                isIl2Cpp = true;
                break;
            }

            staticFields.AddRange(currentType.GetFields(BindingFlags.Static | BindingFlags.NonPublic).Where(x => x.IsInitOnly));
        } while ((currentType = currentType.BaseType) != null);
        
        if (!isIl2Cpp)
            return lookup;

        var fieldPtrs = staticFields.Where(x => x.Name.StartsWith("NativeFieldInfoPtr_"))
                                    .Select(x => new { trimmed = x.Name.Substring("NativeFieldInfoPtr_".Length), ptrF = x });

        var usedFields = new HashSet<MethodInfo>();

        // todo BUGGED, doesn't catch all fields. try doing get field from utils instead?
        foreach (var fieldPtr in fieldPtrs)
        {
            var targetFieldName = fieldPtr.trimmed;
            // Fields are props in il2cpp interop
            var targetField = type.GetProperty(targetFieldName, AccessTools.all);
            if (targetField != null)
            {
                lookup[targetField] = fieldPtr.ptrF;

                var getMethod = targetField.GetGetMethod();
                if (getMethod != null) usedFields.Add(getMethod);
                var setMethod = targetField.GetSetMethod();
                if (setMethod != null) usedFields.Add(setMethod);
            }
        }

        foreach (var propertyInfo in type.GetAllProperties(false).Concat(type.GetAllProperties(true)))
        {
            // It's a field
            if (lookup.ContainsKey(propertyInfo))
                continue;

            var getMethod = propertyInfo.GetGetMethod(true);
            if (getMethod != null && getMethod.GetMethodBody() != null)
            {
                var ptr = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(getMethod);
                if (ptr != null)
                    lookup[getMethod] = ptr;
            }

            var setMethod = propertyInfo.GetSetMethod();
            if (setMethod != null && setMethod.GetMethodBody() != null)
            {
                var ptr = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(setMethod);
                if (ptr != null)
                    lookup[setMethod] = ptr;
            }
        }

        foreach (var methodInfo in type.GetAllMethods(false).Concat(type.GetAllMethods(true)))
        {
            if (lookup.ContainsKey(methodInfo) || usedFields.Contains(methodInfo))
                continue;

            if (methodInfo.GetMethodBody() != null)
            {
                var ptr = Il2CppInterop.Common.Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(methodInfo);
                if (ptr != null)
                    lookup[methodInfo] = ptr;
            }
        }

        return lookup;
    }

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

/// <inheritdoc />
public class IL2CPPFieldCacheEntry : PropertyCacheEntry
{
    public FieldInfo PtrField { get; }

    /// <inheritdoc />
    public IL2CPPFieldCacheEntry(object ins, PropertyInfo p, Type owner, FieldInfo ptrField) : base(ins, p, owner)
    {
        PtrField = ptrField;
        _nameContent.tooltip = $"IL2CPP Field (ptr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrField)})\n\n{_nameContent.tooltip}";
    }

    /// <inheritdoc />
    public IL2CPPFieldCacheEntry(object ins, PropertyInfo p, Type owner, FieldInfo ptrField, ICacheEntry parent) : base(ins, p, owner, parent)
    {
        PtrField = ptrField;
        _nameContent.tooltip = $"IL2CPP Field (ptr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrField)})\n\n{_nameContent.tooltip}";
    }
}

/// <inheritdoc />
public class IL2CPPPropertyCacheEntry : PropertyCacheEntry
{
    public FieldInfo PtrFieldGet { get; }
    public FieldInfo PtrFieldSet { get; }

    /// <inheritdoc />
    public IL2CPPPropertyCacheEntry(object ins, PropertyInfo p, Type owner, FieldInfo ptrFieldGet, FieldInfo ptrFieldSet) : base(ins, p, owner)
    {
        PtrFieldGet = ptrFieldGet;
        PtrFieldSet = ptrFieldSet;
        _nameContent.tooltip = $"IL2CPP Property (getPtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldGet)}, setPtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldSet)})\n\n{_nameContent.tooltip}";
    }
    /// <inheritdoc />
    public IL2CPPPropertyCacheEntry(object ins, PropertyInfo p, Type owner, FieldInfo ptrFieldGet, FieldInfo ptrFieldSet, ICacheEntry parent) : base(ins, p, owner, parent)
    {
        PtrFieldGet = ptrFieldGet;
        PtrFieldSet = ptrFieldSet;
        _nameContent.tooltip = $"IL2CPP Property (getPtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldGet)}, setPtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldSet)})\n\n{_nameContent.tooltip}";
    }
}

/// <inheritdoc />
public class IL2CPPMethodCacheEntry : MethodCacheEntry
{
    public FieldInfo PtrField { get; }

    /// <inheritdoc />
    public IL2CPPMethodCacheEntry(object instance, MethodInfo methodInfo, Type owner, FieldInfo ptrField) : base(instance, methodInfo, owner)
    {
        PtrField = ptrField;
        _nameContent.tooltip = $"IL2CPP Method (ptr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrField)})\n\n{_nameContent.tooltip}";
    }
}

/// <inheritdoc />
/// TODO: This does nothing so far because events are not implemented in il2cpp interop (they show up as separate add/remove/raise methods). Maybe combine them back into events?
public class IL2CPPEventCacheEntry : EventCacheEntry
{
    public FieldInfo PtrFieldAdd { get; }
    public FieldInfo PtrFieldRaise { get; }
    public FieldInfo PtrFieldRemove { get; }
    /// <inheritdoc />
    public IL2CPPEventCacheEntry(object ins, EventInfo e, Type owner, FieldInfo ptrFieldAdd, FieldInfo ptrFieldRaise, FieldInfo ptrFieldRemove) : base(ins, e, owner)
    {
        PtrFieldAdd = ptrFieldAdd;
        PtrFieldRaise = ptrFieldRaise;
        PtrFieldRemove = ptrFieldRemove;
        _nameContent.tooltip = $"IL2CPP Event (addPtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldAdd)}, raisePtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldRaise)}, removePtr={IL2CPPCacheEntryHelper.SafeGetPtr(owner, ptrFieldRemove)})\n\n{_nameContent.tooltip}";
    }
}

#endif
