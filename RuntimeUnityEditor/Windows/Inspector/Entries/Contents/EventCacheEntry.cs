using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class EventCacheEntry : CacheEntryBase
    {
        [Obsolete]
        public object Instance => OwnerInstance;
        public EventInfo EventInfo { get; }
        public EventCacheEntry(object ownerInstance, EventInfo e, Type owner) : base(FieldCacheEntry.GetMemberName(ownerInstance, e), e.GetFancyDescription(), owner, ownerInstance)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            EventInfo = e ?? throw new ArgumentNullException(nameof(e));
            BackingField = owner.GetField(e.Name, BindingFlags.NonPublic | (ownerInstance == null ? BindingFlags.Static : BindingFlags.Instance));
        }

        public FieldInfo BackingField { get; }
        public override bool CanEnterValue() => BackingField != null;
        public override object GetValueToCache() => BackingField?.GetValue(OwnerInstance);
        protected override bool OnSetValue(object newValue) => throw new InvalidOperationException();
        public override Type Type() => EventInfo.EventHandlerType;
        public override bool CanSetValue() => false;
        public bool IsDeclared => Owner == EventInfo.DeclaringType;
    }
}
