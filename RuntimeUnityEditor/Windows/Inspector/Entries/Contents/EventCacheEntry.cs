using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class EventCacheEntry : CacheEntryBase
    {
        public object Instance { get; }
        public EventInfo EventInfo { get; }
        public EventCacheEntry(object ins, EventInfo e, Type owner) : base(FieldCacheEntry.GetMemberName(ins, e), e.GetFancyDescription(), owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Instance = ins;
            EventInfo = e ?? throw new ArgumentNullException(nameof(e));
            BackingField = owner.GetField(e.Name, BindingFlags.NonPublic | (ins == null ? BindingFlags.Static : BindingFlags.Instance));
        }

        public FieldInfo BackingField { get; }
        public override bool CanEnterValue() => BackingField != null;
        public override object GetValueToCache() => BackingField?.GetValue(Instance);
        protected override bool OnSetValue(object newValue) => throw new InvalidOperationException();
        public override Type Type() => EventInfo.EventHandlerType;
        public override bool CanSetValue() => false;
        public bool IsDeclared => Owner == EventInfo.DeclaringType;
    }
}
