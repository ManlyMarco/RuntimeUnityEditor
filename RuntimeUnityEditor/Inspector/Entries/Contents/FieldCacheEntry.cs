using System;
using System.Reflection;

namespace RuntimeUnityEditor.Core.Inspector.Entries
{
    public class FieldCacheEntry : CacheEntryBase
    {
        public FieldCacheEntry(object ins, FieldInfo f) : this(ins, f, null) { }
        public FieldCacheEntry(object ins, FieldInfo f, ICacheEntry parent) : base(GetMemberName(ins, f))
        {
            if (f == null)
                throw new ArgumentNullException(nameof(f));

            _instance = ins;
            FieldInfo = f;
            _parent = parent;
        }

        internal static string GetMemberName(object ins, MemberInfo f)
        {
            if (ins != null) return f?.Name;
            return "S/" + f?.Name;
        }

        public FieldInfo FieldInfo { get; }
        private readonly object _instance;
        private readonly ICacheEntry _parent;

        public override object GetValueToCache()
        {
            return FieldInfo.GetValue(_instance);
        }

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
