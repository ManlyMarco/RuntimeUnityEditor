#if IL2CPP
using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

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
#endif