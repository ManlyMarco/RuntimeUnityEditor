#if IL2CPP
using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

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
#endif