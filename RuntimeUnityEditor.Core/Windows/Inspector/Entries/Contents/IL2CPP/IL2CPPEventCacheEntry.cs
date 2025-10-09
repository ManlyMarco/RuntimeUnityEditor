#if IL2CPP
using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Inspector.Entries;

namespace RuntimeUnityEditor.Core.Inspector.IL2CPP;

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