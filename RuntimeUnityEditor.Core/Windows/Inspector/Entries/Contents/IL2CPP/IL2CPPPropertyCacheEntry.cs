#if IL2CPP
using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

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
#endif