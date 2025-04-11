using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    /// <inheritdoc/>
    public class EventCacheEntry : CacheEntryBase
    {
        /// <summary>
        /// Instance of the object that owns the event.
        /// </summary>
        public object Instance { get; }

        /// <summary>
        /// EventInfo for the event.
        /// </summary>
        public EventInfo EventInfo { get; }

        /// <inheritdoc/>
        public EventCacheEntry(object ins, EventInfo e, Type owner) : base(FieldCacheEntry.GetMemberName(ins, e), e.GetFancyDescription(), owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Instance = ins;
            EventInfo = e ?? throw new ArgumentNullException(nameof(e));
            BackingField = owner.GetField(e.Name, BindingFlags.NonPublic | (ins == null ? BindingFlags.Static : BindingFlags.Instance));
        }

        /// <summary>
        /// Backing field for the event. This is used to inspect the event.
        /// </summary>
        public FieldInfo BackingField { get; }

        /// <inheritdoc/>
        public override bool CanEnterValue() => BackingField != null;
        /// <inheritdoc/>
        public override object GetValueToCache() => BackingField?.GetValue(Instance);
        /// <inheritdoc/>
        protected override bool OnSetValue(object newValue) => throw new InvalidOperationException();
        /// <inheritdoc/>
        public override Type Type() => EventInfo.EventHandlerType;
        /// <inheritdoc/>
        public override MemberInfo MemberInfo => EventInfo;
        /// <inheritdoc/>
        public override bool CanSetValue() => false;

        /// <summary>
        /// Checks if the event is declared in the owner type.
        /// </summary>
        public bool IsDeclared => Owner == EventInfo.DeclaringType;
    }
}
