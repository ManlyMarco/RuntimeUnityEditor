using System;
using System.Reflection;
using RuntimeUnityEditor.Core.Utils;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class FieldCacheEntry : CacheEntryBase
    {
        public FieldCacheEntry(object ins, FieldInfo f, Type owner, object ownerInstance) : this(ins, f, owner, ownerInstance, null) { }
        public FieldCacheEntry(object ins, FieldInfo f, Type owner, object ownerInstance, ICacheEntry parent) : base(GetMemberName(ins, f), f.GetFancyDescription(), owner, ownerInstance)
        {
            _instance = ins;
            FieldInfo = f ?? throw new ArgumentNullException(nameof(f));
            _parent = parent;
        }

        internal static string GetMemberName(object ins, MemberInfo f)
        {
            if (ins != null)
                return f?.Name;
            return "S/" + f?.Name;
        }

        public FieldInfo FieldInfo { get; }
        public bool IsDeclared => Owner == FieldInfo.DeclaringType;
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        public override object GetValueToCache() => FieldInfo.GetValue(_instance);

        protected override bool OnSetValue(object newValue)
        {
            if (!FieldInfo.IsInitOnly)
            {
                FieldInfo.SetValue(_instance, newValue);
                // Needed for structs to propagate changes back to the original field/prop
                if (_parent != null && _parent.CanSetValue()) _parent.SetValue(_instance);
                return true;
            }
            return false;
        }

        public override Type Type()
        {
            return FieldInfo.FieldType;
        }

        public override bool CanSetValue()
        {
            return (FieldInfo.Attributes & FieldAttributes.Literal) == 0 && !FieldInfo.IsInitOnly && (_parent == null || _parent.CanSetValue());
        }
    }
}
